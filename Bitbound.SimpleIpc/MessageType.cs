using System.Runtime.Serialization;

namespace Bitbound.SimpleIpc;

[DataContract]
public enum MessageType
{
  [EnumMember]
  Unspecified,

  [EnumMember]
  Send,

  [EnumMember]
  Invoke,

  [EnumMember]
  Response
}