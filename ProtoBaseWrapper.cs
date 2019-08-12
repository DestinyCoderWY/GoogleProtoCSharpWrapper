using System.Collections;
using System.Collections.Generic;

[System.Serializable()]
public abstract class ProtoBaseWrapper<T> where T : Google.Protobuf.IMessage<T> ,new()
{
    public abstract void LoadBaseToWrapper(T baseInfo);

    public abstract T OutputBaseFromWrapper();
}
