/*
 * CSharpBcpRpc
 * Copyright 2014 深圳岂凡网络有限公司 (Shenzhen QiFun Network Corp., LTD)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Qifun.BcpRpc
{
    public sealed class RpcDelegate
    {
        public delegate TResponseMessage RequestCallback<TRequestMessage, TResponseMessage, TSession>(TRequestMessage message, TSession session)
            where TRequestMessage : IExtensible
            where TResponseMessage : IExtensible 
            where TSession : RpcSession;

        /// <summary>
        ///  Callback for Event, Info and CastRequest
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="TService"></typeparam>
        /// <param name="message"></param>
        /// <param name="service"></param>
        public delegate void MessageCallback<TMessage, TSession>(TMessage message, TSession service)
            where TMessage : IExtensible where TSession : RpcSession;

    }
}
