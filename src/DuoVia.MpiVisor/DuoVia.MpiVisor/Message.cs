﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.Net;

namespace DuoVia.MpiVisor
{
    [Serializable]
    public class Message
    {
        public Message(Guid sessionId, ushort fromId, ushort toId, int contentType, object content)
        {
            this.SessionId = sessionId;
            this.FromId = fromId;
            this.ToId = toId;
            this.MessageType = contentType;
            this.Content = content;
        }

        public Message() { }

        /// <summary>
        /// The execution context session id.
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// The agent id the message is from.
        /// </summary>
        public ushort FromId { get; set; }

        /// <summary>
        /// Is a broadcast message.
        /// </summary>
        public bool IsBroadcast { get { return (ToId == MpiConsts.BroadcastAgentId); } }

        /// <summary>
        /// AgentId message is sent to. ushort.MaxValue 65535 is used for broadcast to all agents except sender.
        /// </summary>
        public ushort ToId { get; set; } 

        /// <summary>
        /// Should be a positive value, generally aligned with an application enum. 
        /// Negative values are reserved for system message types. See SystemMessageTypes.
        /// </summary>
        public int MessageType { get; set; }

        /// <summary>
        /// Message content.
        /// </summary>
        public object Content
        {
            get
            {
                if (_contentBytes == null) return null;
                if (_contentBytes.Length == 0) return _contentBytes;
                if (_contentObject == null) _contentObject = _contentBytes.ToDeserializedObject();
                return _contentObject;
            }
            set
            {
                _contentObject = value;
                if (null == _contentObject)
                    _contentBytes = null;
                else
                    _contentBytes = _contentObject.ToSerializedBytes();
            }
        }

        //serialized message content
        private byte[] _contentBytes { get; set; }

        [NonSerialized]
        private object _contentObject = null; //deserialized object to prevent multiple deserializations
    }
}
