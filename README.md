# GoogleProtobufCSharpWrapper

    由于从Google的proto文件自动化生成的C#脚本中所有变量都设置为属性(Property)，无法暴露给Unity编辑器进行编辑
    所以设计了一个脚本，读取proto文件编译出的pb文件，自动化生成相对应的Wrapper
    其中提供Load和Output函数用于读取（指数据从Google C#到Wrapper）与输出（指数据从Wrapper到Google C#）
    以此可以将Google C#所读取的数据传给Wrapper以暴露给Unity编辑器，或是Unity编辑器编辑后存于Wrapper传给Google C#，并进行.bytes文件的保存

##使用说明
* 通过main中调用**Program.ParsePB(参数1, 参数2)** 来进行Wrapper文件的自动化生成（第一个参数为生成的pb目录，第二个参数为输出Wrapper的目录）
* 所有生成的Wrapper类都会继承**ProtoBaseWrapper** 这个基类，可用于写泛型调用，该类存于**ProtoBaseWrapper.cs** 中。如果不需要继承基类，则在**Program.cs** 的**AddClass** 中删除。（可查看注释**添加基类**以定位代码）