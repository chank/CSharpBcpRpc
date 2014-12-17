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

namespace Qifun.BcpRpc
{
    public abstract class RpcException : Exception
    {
        public RpcException(string message, Exception cause) : base(message, cause)
        {
        }
    }

    public class IllegalRpcData : RpcException
    {
        public IllegalRpcData(string message = null, Exception cause = null)
            : base(message, cause)
        {
        }
    }

    public class UnknowServiceName : RpcException
    {
        public UnknowServiceName(string message = null, Exception cause = null)
            : base(message, cause)
        {
        }
    }

    public class ParseTextException: RpcException
    {
        public ParseTextException(string message = null, Exception cause = null)
            : base(message, cause)
        { 
        }
    }
    
}
