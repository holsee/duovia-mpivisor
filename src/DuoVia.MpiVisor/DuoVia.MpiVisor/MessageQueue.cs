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
        Message ReceiveAnyMessage(int timeoutSeconds = int.MaxValue);
        Message ReceiveApplicationMessage(int timeoutSeconds = int.MaxValue);
        Message ReceiveFilteredMessage(int contentType, int timeoutSeconds = 90);
        Message ReceiveFilteredMessage(ushort fromAgentId, int timeoutSeconds = 90);
        void Send(Message message);
        void Send(ushort toAgentId, int messageType, object content);
        bool AllowMessageEnqueing { get; set; }
    }

    public class MessageQueue : IMessageQueue, IMessageQueueWriter
    {
        private INodeService _nodeServiceProxy = null;
        private ushort _agentId = 0;
        private SessionInfo _session = null;

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

        public bool AllowMessageEnqueing { get; set; }

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

        /// <summary>
        /// Returns first message in queue when received. Blocking call with timeout safety valve.
        /// </summary>
        /// <param name="timeoutSeconds">If no message received in timeoutSeconds, a message with
        /// default values and null content is returned.</param>
        /// <returns></returns>
        public Message ReceiveAnyMessage(int timeoutSeconds = int.MaxValue)
        {
            var entered = DateTime.Now;
            while (AllowMessageEnqueing && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
            {
                _incomingMessageWaitHandle.WaitOne();
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
            while (AllowMessageEnqueing && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
            {
                _incomingMessageWaitHandle.WaitOne();
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
            while (AllowMessageEnqueing && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
            {
                _incomingMessageWaitHandle.WaitOne();
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
            return MakeNullMessage();
        }

        /// <summary>
        /// Returns first message in queue when received from for an application level content type. Blocking call with timeout safety valve.
        /// </summary>
        /// <param name="timeoutSeconds">If no message received in timeoutSeconds, a message with
        /// default values and null content is returned.
        /// If no message received in timeoutSeconds, a null be returned.</param>
        /// <returns></returns>
        public Message ReceiveApplicationMessage(int timeoutSeconds = int.MaxValue)
        {
            var entered = DateTime.Now;
            while (AllowMessageEnqueing && (entered - DateTime.Now).TotalSeconds < timeoutSeconds)
            {
                _incomingMessageWaitHandle.WaitOne();
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
                    if (message.MessageType == -999999)
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
            var msg = new Message(_session.SessionId, _agentId, _agentId, -987654, null);
            return msg;
        }

        public void Dispose()
        {
            _incomingMessageWaitHandle.Dispose();
        }
    }
}
