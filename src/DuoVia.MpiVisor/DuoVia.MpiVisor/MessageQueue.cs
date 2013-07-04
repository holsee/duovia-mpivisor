using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.MpiVisor.Services;
using System.Threading;

namespace DuoVia.MpiVisor
{
    public interface IMessageQueueWriter
    {
        void EnqueuMessage(Message message);
    }

    public interface IMessageQueue : IDisposable
    {
        void Broadcast(int messageType, object content);
        Message ReceiveAnyMessage(int timeoutSeconds = 3600); //max one hour before null message returned
        Message ReceiveApplicationMessage(int timeoutSeconds = 3600);
        Message ReceiveFilteredMessage(int contentType, int timeoutSeconds = 90);
        Message ReceiveFilteredMessage(ushort fromAgentId, int timeoutSeconds = 90);
        void Send(Message message);
        void Send(ushort toAgentId, int messageType, object content);
        bool AllowMessageEnqueing { get; set; }
        Func<int, bool> AbortMessageWaitVisitor { get; set; }
        int WaitForMessageAbortTimeMs { get; set; }
    }

    public class MessageQueue : IMessageQueue, IMessageQueueWriter
    {
        private INodeService _nodeServiceProxy = null;
        private ushort _agentId = 0;
        private SessionInfo _session = null;

        private int _waitForMessageAbortTimeMs = 1000;
        private Func<int, bool> _abortMessageWaitVisitor = null;

        //incoming message queue and reset event to signal blocking receive message methods
        //the EnqueuMessage method is called by the thread running the agent service which
        //signals the event so that the receive message method can continue and return the message
        private ManualResetEvent _incomingMessageWaitHandle = new ManualResetEvent(false);
        private LinkedList<Message> _incomingMessageBuffer = new LinkedList<Message>();

        public MessageQueue(ushort agentId, SessionInfo session, INodeService nodeServiceProxy)
        {
            _agentId = agentId;
            _session = session;
            _nodeServiceProxy = nodeServiceProxy;
            AllowMessageEnqueing = true;
        }

        /// <summary>
        /// Set to false to prevent further message reception.
        /// </summary>
        public bool AllowMessageEnqueing { get; set; }

        /// <summary>
        /// Set function to evaluate whether a message wait should be abandoned and a null message returned.
        /// </summary>
        public Func<int, bool> AbortMessageWaitVisitor
        {
            get { return _abortMessageWaitVisitor; }
            set { _abortMessageWaitVisitor = value; }
        }

        /// <summary>
        /// Set time in milliseconds that the message recive method will wait before executing the AbortMessageWaitVisitor function
        /// to determine whether a null message should be returned to allow the message loop to deal with
        /// the possibility of an agent being abandoned. Min 100 and max 3,600,000.
        /// </summary>
        public int WaitForMessageAbortTimeMs
        {
            get
            {
                return _waitForMessageAbortTimeMs;
            }
            set
            {
                if (value < 100)
                    _waitForMessageAbortTimeMs = 100;
                else if (value > 3600000)
                    _waitForMessageAbortTimeMs = 3600000;
                else
                    _waitForMessageAbortTimeMs = value;
            }
        }

        /// <summary>
        /// Broadcast message to all other running agents.
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="content"></param>
        public void Broadcast(int messageType, object content)
        {
            var msg = new Message
            {
                FromId = _agentId,
                SessionId = _session.SessionId,
                ToId = MpiConsts.BroadcastAgentId,
                MessageType = messageType,
                Content = content
            };
            _nodeServiceProxy.Broadcast(msg);
        }

        /// <summary>
        /// Send message to another worker agent in this execution context session.
        /// </summary>
        /// <param name="message"></param>
        public void Send(Message message)
        {
            _nodeServiceProxy.Send(message);
        }

        /// <summary>
        /// Send message to another agent from this agent.
        /// </summary>
        /// <param name="toAgentId"></param>
        /// <param name="messageType"></param>
        /// <param name="content"></param>
        public void Send(ushort toAgentId, int messageType, object content)
        {
            var msg = new Message
            {
                FromId = _agentId,
                SessionId = _session.SessionId,
                ToId = toAgentId,
                MessageType = messageType,
                Content = content
            };
            Send(msg);
        }

        private bool ShouldAbandonMessageWait(int count)
        {
            bool abandon = false;
            if (_abortMessageWaitVisitor != null)
            {
                try
                {
                    abandon = _abortMessageWaitVisitor(count);
                }
                catch
                {
                    abandon = false;
                }
            }
            return abandon;
        }

        /// <summary>
        /// Returns first message in queue when received. Blocking call with timeout safety valve.
        /// </summary>
        /// <param name="timeoutSeconds">If no message received in timeoutSeconds, a message with
        /// default values and null content is returned.</param>
        /// <returns></returns>
        public Message ReceiveAnyMessage(int timeoutSeconds = 3600)
        {
            var entered = DateTime.Now;
            var count = 0;
            while (AllowMessageEnqueing && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
            {
                count++;
                if (_incomingMessageWaitHandle.WaitOne(_waitForMessageAbortTimeMs)) //eval while once per second when not signaled
                {
                    Message message = null;
                    lock (_incomingMessageBuffer)
                    {
                        LinkedListNode<Message> firstMessage = _incomingMessageBuffer.First;
                        if (firstMessage != null)
                        {
                            message = firstMessage.Value;
                            _incomingMessageBuffer.RemoveFirst();
                        }
                        else
                        {
                            //set to nonsignaled and block on WaitOne again
                            _incomingMessageWaitHandle.Reset();
                        }
                    }
                    if (null != message)
                    {
                        return message;
                    }
                }
                else if (ShouldAbandonMessageWait(count)) break; //force null message result
            }
            return MakeNullMessage();
        }

        /// <summary>
        /// Returns first message in queue when received from a specified agent. Blocking call with timeout safety valve.
        /// </summary>
        /// <param name="fromAgentId">The agent id from whom a message is expected.</param>
        /// <param name="timeoutSeconds">If no message received in timeoutSeconds, a message with
        /// default values and null content is returned.
        /// Since this is filtered, the default is on 90 seconds.</param>
        /// <returns></returns>
        public Message ReceiveFilteredMessage(ushort fromAgentId, int timeoutSeconds = 90)
        {
            var entered = DateTime.Now;
            var count = 0;
            while (AllowMessageEnqueing && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
            {
                count++;
                if (_incomingMessageWaitHandle.WaitOne(_waitForMessageAbortTimeMs)) //eval while once per second when not signaled
                {
                    Message message = null;
                    lock (_incomingMessageBuffer)
                    {
                        LinkedListNode<Message> nodeMessage = _incomingMessageBuffer.First;
                        while (null == message && null != nodeMessage && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
                        {
                            if (nodeMessage.Value.FromId == fromAgentId)
                            {
                                message = nodeMessage.Value;
                                _incomingMessageBuffer.Remove(nodeMessage);
                            }
                            else
                                nodeMessage = nodeMessage.Next;
                        }
                        if (null == message) _incomingMessageWaitHandle.Reset(); //none found
                    }
                    if (null != message)
                    {
                        return message;
                    }
                }
                else if (ShouldAbandonMessageWait(count)) break; //force null message result
            }
            return MakeNullMessage();
        }

        /// <summary>
        /// Returns first message in queue when received from for a specified content type. Blocking call with timeout safety valve.
        /// </summary>
        /// <param name="contentType">The content type of the message expected.</param>
        /// <param name="timeoutSeconds">If no message received in timeoutSeconds, a message with
        /// default values and null content is returned.
        /// Since this is filtered, the default is on 90 seconds.</param>
        /// <returns></returns>
        public Message ReceiveFilteredMessage(int contentType, int timeoutSeconds = 90)
        {
            var entered = DateTime.Now;
            var count = 0;
            while (AllowMessageEnqueing && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
            {
                count++;
                if (_incomingMessageWaitHandle.WaitOne(_waitForMessageAbortTimeMs)) //eval while once per second when not signaled
                {
                    Message message = null;
                    lock (_incomingMessageBuffer)
                    {
                        LinkedListNode<Message> nodeMessage = _incomingMessageBuffer.First;
                        while (null == message && null != nodeMessage && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
                        {
                            if (nodeMessage.Value.MessageType == contentType)
                            {
                                message = nodeMessage.Value;
                                _incomingMessageBuffer.Remove(nodeMessage);
                            }
                            else
                                nodeMessage = nodeMessage.Next;
                        }
                        if (null == message) _incomingMessageWaitHandle.Reset(); //none found
                    }
                    if (null != message)
                    {
                        return message;
                    }
                }
                else if (ShouldAbandonMessageWait(count)) break; //force null message result
            }
            return MakeNullMessage();
        }

        /// <summary>
        /// Returns first message in queue when received from for an application level content type. Blocking call with timeout safety valve.
        /// </summary>
        /// <param name="timeoutSeconds">If no message received in timeoutSeconds, a message with
        /// default values and null content is returned.
        /// If no message received in timeoutSeconds, a null be returned.</param>
        /// <returns></returns>
        public Message ReceiveApplicationMessage(int timeoutSeconds = 3600)
        {
            var entered = DateTime.Now;
            var count = 0;
            while (AllowMessageEnqueing && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
            {
                count++;
                if (_incomingMessageWaitHandle.WaitOne(_waitForMessageAbortTimeMs)) //eval while once per second when not signaled
                {
                    Message message = null;
                    lock (_incomingMessageBuffer)
                    {
                        LinkedListNode<Message> nodeMessage = _incomingMessageBuffer.First;
                        while (null == message && null != nodeMessage && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
                        {
                            if (nodeMessage.Value.MessageType > -1)
                            {
                                message = nodeMessage.Value;
                                _incomingMessageBuffer.Remove(nodeMessage);
                            }
                            else
                                nodeMessage = nodeMessage.Next;
                        }
                        if (null == message) _incomingMessageWaitHandle.Reset(); //none found
                    }
                    if (null != message)
                    {
                        return message;
                    }
                }
                else if (ShouldAbandonMessageWait(count)) break; //force null message result
            }
            return MakeNullMessage();
        }

        /// <summary>
        /// Adds message to incoming queue to be "received". 
        /// Called by agent service to deliver messages.
        /// </summary>
        /// <param name="message"></param>
        public void EnqueuMessage(Message message)
        {
            lock (_incomingMessageBuffer)
            {
                if (AllowMessageEnqueing)
                {
                    if (message.MessageType == -999999) //do not deliver log shuttle message
                    {
                        if (message.Content != null) Log.LogMessage(message.Content.ToString());
                    }
                    else
                    {
                        _incomingMessageBuffer.AddLast(message);
                        _incomingMessageWaitHandle.Set();
                    }
                }
            }
        }

        /// <summary>
        /// Create the message that is returned when Receive methods timeout.
        /// </summary>
        /// <returns></returns>
        private Message MakeNullMessage()
        {
            //make a message that is from and to this agent with unused message type value
            var msg = new Message(_session.SessionId, _agentId, _agentId, SystemMessageTypes.NullMessage, null);
            return msg;
        }

        public void Dispose()
        {
            _incomingMessageWaitHandle.Dispose();
        }
    }
}
