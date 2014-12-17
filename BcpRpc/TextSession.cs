/*
 * csharp-bcp-rpc
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using haxe.lang;
using com.qifun.jsonStream.io;
using com.qifun.jsonStream;
using com.qifun.jsonStream.rpc;
using System.Threading;
using Qifun.Bcp;
using System.Diagnostics;

using haxe.root;

namespace Qifun.BcpRpc
{
    public abstract class TextSession<BcpSession> : RpcSession<BcpSession> where BcpSession : Bcp.BcpSession
    {
        public TextSession(Bcp.BcpSession bcpSession)
            : base()
        {
            this.bcpSession = bcpSession;
            this.bcpSession.Received += OnReceived;
        }
        public readonly Bcp.BcpSession bcpSession;

        protected override IList<ArraySegment<byte>> ToByteBuffer(com.qifun.jsonStream.JsonStream js)
        {
            var output = new ArraySegmentOutput();
            PrettyTextPrinter.print(output, js, Null<int>._ofDynamic(0));
            return output.Buffers;
        }

        protected override JsonStream ToJsonStream(IList<ArraySegment<byte>> buffers)
        {
            var arraySegmentInput = new ArraySegmentInput(buffers);
            JsonStream jsonStream = null;
            try
            {
                jsonStream = TextParser.parseInput(new ArraySegmentInput(buffers));
            }
            catch (Exception e)
            {
                throw new ParseTextException("Parse exception: ", e);
            }
            return jsonStream;
        }

        private class JsonService : IJsonService
        {
            private string serviceClassName;
            private TextSession<BcpSession> textSession;

            public JsonService(TextSession<BcpSession> textSession, string serviceClassName)
            {
                this.serviceClassName = serviceClassName;
                this.textSession = textSession;
            }

            public void push(JsonStream data)
            {
                var pushStream = JsonStream.OBJECT(Generator1(new JsonStreamPair(
                    "push",
                    JsonStream.OBJECT(Generator1(new JsonStreamPair(serviceClassName, data))))));
                textSession.bcpSession.Send(textSession.ToByteBuffer(pushStream));
            }

            public void apply(JsonStream request, IJsonResponseHandler handler)
            {
                int requestId = Interlocked.Increment(ref textSession.nextRequestId);
                IJsonResponseHandler oldHandler;
                lock (textSession.outgoingRpcResponseHandlerLock)
                {
                    if (textSession.outgoingRpcResponseHandlers.TryGetValue(requestId, out oldHandler))
                    {
                        throw new InvalidOperationException();
                    }
                    else
                    {
                        textSession.outgoingRpcResponseHandlers.Add(requestId, handler);
                    }
                }
                var requestStream = JsonStream.OBJECT(Generator1(new JsonStreamPair(
                    "request",
                    JsonStream.OBJECT(Generator1(new JsonStreamPair(
                        requestId.ToString(),
                        JsonStream.OBJECT(Generator1(new JsonStreamPair(serviceClassName, request)))))))));
                textSession.bcpSession.Send(textSession.ToByteBuffer(requestStream));
            }
        }

        public ServiceInterface OutgoingService<ServiceInterface>(OutgoingProxyEntry<ServiceInterface> entry)
        {
            var serviceClassName = entry.serviceType.ToString();
            return entry.outgoingView(new JsonService(this, serviceClassName));
        }

        private sealed class JsonResponseHandler : IJsonResponseHandler
        {
            private string id;
            private TextSession<BcpSession> textSession;

            public JsonResponseHandler(TextSession<BcpSession> textSession, string id)
            {
                this.textSession = textSession;
                this.id = id;
            }

            public void onSuccess(JsonStream responseBody)
            {
                var responseStream = JsonStream.OBJECT(Generator1(new JsonStreamPair(
                    "success",
                    JsonStream.OBJECT(Generator1(new JsonStreamPair(
                        id,
                        responseBody))))));
                textSession.bcpSession.Send(textSession.ToByteBuffer(responseStream));
            }

            public void onFailure(JsonStream errorBody)
            {
                var responseStream = JsonStream.OBJECT(Generator1(new JsonStreamPair(
                    "failure",
                    JsonStream.OBJECT(Generator1(new JsonStreamPair(
                        id,
                        errorBody))))));
                textSession.bcpSession.Send(textSession.ToByteBuffer(responseStream));
            }
        }

        private static bool ReflectHasNext(object iterator)
        {
            return Runtime.toBool(Reflect.callMethod(iterator, Reflect.field(iterator, "hasNext"), new Array<object>()));
        }

        private static Element ReflectNext<Element>(object iterator)
        {
            return Runtime.genericCast<Element>(Reflect.callMethod(iterator, Reflect.field(iterator, "next"), new Array<object>()));
        }

        private static readonly int jsonStreamObjectIndex = haxe.root.Type.getEnumConstructs(typeof(JsonStream)).indexOf("OBJECT", Null<int>._ofDynamic(0));

        private void OnReceived(object sender, Bcp.BcpSession.ReceivedEventArgs e)
        {
            var jsonStream = ToJsonStream(e.Buffers);
            if (haxe.root.Type.enumIndex(jsonStream) == jsonStreamObjectIndex)
            {
                var requestOrResponsePairs = haxe.root.Type.enumParameters(jsonStream)[0];
                while (ReflectHasNext(requestOrResponsePairs))
                {
                    var requestOrResponsePair = ReflectNext<JsonStreamPair>(requestOrResponsePairs);
                    if (haxe.root.Type.enumIndex(requestOrResponsePair.value) == jsonStreamObjectIndex)
                    {
                        switch (requestOrResponsePair.key)
                        {
                            case "push":
                                {
                                    var servicePairs = haxe.root.Type.enumParameters(requestOrResponsePair.value)[0];
                                    while (ReflectHasNext(servicePairs))
                                    {
                                        var servicePair = ReflectNext<JsonStreamPair>(servicePairs);
                                        RpcDelegate.IncomingProxyCallback<RpcSession<BcpSession>> incomingRpc;
                                        if (IncomingServices.incomingProxyMap.TryGetValue(servicePair.key, out incomingRpc))
                                        {
                                            incomingRpc(this).push(servicePair.value);
                                        }
                                        else
                                        {
                                            this.bcpSession.Interrupt();
                                            Debug.WriteLine("Unkown service name");
                                        }
                                    }
                                }
                                break;
                            case "request":
                                {
                                    var idPaires = haxe.root.Type.enumParameters(requestOrResponsePair.value)[0];
                                    while (ReflectHasNext(idPaires))
                                    {
                                        var idPair = ReflectNext<JsonStreamPair>(idPaires);
                                        var id = idPair.key;
                                        if (haxe.root.Type.enumIndex(idPair.value) == jsonStreamObjectIndex)
                                        {
                                            var servicePairs = haxe.root.Type.enumParameters(idPair.value)[0];
                                            while (ReflectHasNext(servicePairs))
                                            {
                                                var servicePair = ReflectNext<JsonStreamPair>(servicePairs);
                                                RpcDelegate.IncomingProxyCallback<RpcSession<BcpSession>> incomingRpc;
                                                if (IncomingServices.incomingProxyMap.TryGetValue(servicePair.key, out incomingRpc))
                                                {
                                                    incomingRpc(this).apply(servicePair.value, new JsonResponseHandler(this, id));
                                                }
                                                else
                                                {
                                                    this.bcpSession.Interrupt();
                                                    Debug.WriteLine("Unkown service name");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            this.bcpSession.Interrupt();
                                            Debug.WriteLine("Illegal rpc data!");
                                        }
                                    }
                                }
                                break;
                            case "failure":
                                {
                                    var idPairs = haxe.root.Type.enumParameters(requestOrResponsePair.value)[0];
                                    while (ReflectHasNext(idPairs))
                                    {
                                        var idPair = ReflectNext<JsonStreamPair>(idPairs);
                                        int id;
                                        try
                                        {
                                            id = Convert.ToInt32(idPair.key);
                                        }
                                        catch (Exception exception)
                                        {
                                            this.bcpSession.Interrupt();
                                            throw new IllegalRpcData("", exception);
                                        }
                                        IJsonResponseHandler handler;
                                        lock (outgoingRpcResponseHandlerLock)
                                        {
                                            if (outgoingRpcResponseHandlers.TryGetValue(id, out handler))
                                            {
                                                outgoingRpcResponseHandlers.Remove(id);
                                            }
                                            else
                                            {
                                                this.bcpSession.Interrupt();
                                                Debug.WriteLine("Illegal rpc data!");
                                            }
                                        }
                                        handler.onFailure(idPair.value);
                                    }
                                }
                                break;
                            case "success":
                                {
                                    var idPairs = haxe.root.Type.enumParameters(requestOrResponsePair.value)[0];
                                    while (ReflectHasNext(idPairs))
                                    {
                                        var idPair = ReflectNext<JsonStreamPair>(idPairs);
                                        int id;
                                        try
                                        {
                                            id = Convert.ToInt32(idPair.key);
                                        }
                                        catch (Exception exception)
                                        {
                                            this.bcpSession.Interrupt();
                                            throw new IllegalRpcData("", exception);
                                        }
                                        IJsonResponseHandler handler;
                                        lock (outgoingRpcResponseHandlerLock)
                                        {
                                            if (outgoingRpcResponseHandlers.TryGetValue(id, out handler))
                                            {
                                                outgoingRpcResponseHandlers.Remove(id);
                                            }
                                            else
                                            {
                                                this.bcpSession.Interrupt();
                                                Debug.WriteLine("Illegal rpc data!");
                                            }
                                        }
                                        handler.onSuccess(idPair.value);
                                    }
                                }
                                break;
                            default:
                                {
                                    this.bcpSession.Interrupt();
                                    Debug.WriteLine("Illegal rpc data!");
                                }
                                break;
                        }
                    }
                    else
                    {
                        this.bcpSession.Interrupt();
						Debug.WriteLine("Illegal rpc data!");
                    }
                }
            }
            else
            {
                this.bcpSession.Interrupt();
				Debug.WriteLine("Illegal rpc data!");
            }
        }
    }
}
