using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
#pragma warning disable SYSLIB0051

namespace Infrastructure
{
  /// <summary>
  /// Use this exception to indicate that the process has been cancelled by intention, (i.e. by the user)
  /// So we can use the magic of exceptions to gracefully cleanup
  /// (do not use OperationCancelledException, (this is used by async and you won't see the related messages)
  /// </summary>
  [Serializable]
  public class AbortException : Exception
  {
    //
    // For guidelines regarding the creation of new exception types, see
    //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
    // and
    //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
    //

    public AbortException() : base(string.Empty) //avoid "Exception of type 'Infrastructure.AbortException' was thrown."
    {
    }

    public AbortException(string message) : base(message)
    {
    }

    public AbortException(string message, Exception inner) : base(message, inner)
    {
    }

    protected AbortException(
      SerializationInfo info,
      StreamingContext context) : base(info, context)
    {
    }
  }
}
