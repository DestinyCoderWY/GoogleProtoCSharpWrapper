using System;
using System.Collections.Generic;
using System.IO;
using Google.ProtocolBuffers.DescriptorProtos;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;

namespace ProtobufReflection
{
    class Program
    {
        private static Dictionary<FieldDescriptorProto.Types.Type, Type> typeMap;
        private const string baseString = "baseInfo";

        static Program()
        {
            typeMap = new Dictionary<FieldDescriptorProto.Types.Type, Type>
            {
                { FieldDescriptorProto.Types.Type.TYPE_INT32, typeof(int) },
                { FieldDescriptorProto.Types.Type.TYPE_STRING, typeof(string) },
                { FieldDescriptorProto.Types.Type.TYPE_SINT32, typeof(int) },
                { FieldDescriptorProto.Types.Type.TYPE_SINT64, typeof(long) },
                { FieldDescriptorProto.Types.Type.TYPE_FLOAT, typeof(float) },
                { FieldDescriptorProto.Types.Type.TYPE_BOOL, typeof(bool) },
                { FieldDescriptorProto.Types.Type.TYPE_UINT32, typeof(uint) }
            }; 
        }

        public static void ParsePB(string FilePath, string outputFilePath)
        {
            var pbFile = FileDescriptorSet.ParseFrom(File.ReadAllBytes(FilePath));

            var protoList = pbFile.FileList;
            //指代特定proto
            //for (int i = 0; i < protoList.Count; i++)
            //{
            //    Console.WriteLine(i + "  " + protoList[i].Name);
            //    foreach (var message in protoList[i].MessageTypeList)
            //    {
            //        Console.WriteLine("  " + message.Name);
            //if (message.UnknownFields[8].LengthDelimitedList.Count > 0)
            //{
            //    foreach (var str in message.UnknownFields[8].LengthDelimitedList)
            //    {
            //        Console.WriteLine("  " + str.ToStringUtf8().Replace("\n", ""));
            //    }
            //}
            //        for (int j = 0; j < message.FieldList.Count; j++)
            //        {
            //            Console.WriteLine(j + "  " + message.FieldList[j].Name);
            //            Console.WriteLine(j + "  " + message.FieldList[j].Type.ToString());
            //            Console.WriteLine(j + "  " + message.FieldList[j].Label.ToString());

            //        }
            //    }
            //}
            foreach (var proto in protoList)
            {
                ParseProto(proto, outputFilePath);
            }
        }

        public static void ParseProto(FileDescriptorProto proto, string outputFilePath)
        {
            //准备一个代码编译器单元
            CodeCompileUnit codeCompileUnit = new CodeCompileUnit();

            //设置命名空间（这个是指要生成的类的空间）
            CodeNamespace myNamespace = new CodeNamespace("Game.Data");
            //导入必要的命名空间引用
            myNamespace.Imports.Add(new CodeNamespaceImport("System"));
            myNamespace.Imports.Add(new CodeNamespaceImport("Game.Data"));
            myNamespace.Imports.Add(new CodeNamespaceImport("UnityEngine"));
            myNamespace.Imports.Add(new CodeNamespaceImport("System.Collections.Generic"));
            //把该命名空间加入到编译器单元的命名空间集合中
            codeCompileUnit.Namespaces.Add(myNamespace);

            foreach (var message in proto.MessageTypeList)
            {
                AddClass(message, codeCompileUnit, myNamespace);
            }

            //生成C#脚本
            CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");
            CodeGeneratorOptions options = new CodeGeneratorOptions();
            //代码风格:大括号的样式{}
            options.BracingStyle = "C";
            //是否在字段、属性、方法之间添加空白行
            options.BlankLinesBetweenMembers = true;

            string name = GenerateCamelString(proto.Name.Split('.')[0].Split('_'));

            //输出文件路径
            string outputFile = $"{outputFilePath}/{name}Wrapper.cs";
            Console.WriteLine($"out: {outputFile}");

            //去只读
            if (File.Exists(outputFile))
            {
                File.SetAttributes(outputFile, FileAttributes.Normal);
            }

            //保存
            using (StreamWriter streamWriter = new StreamWriter(outputFile))
            {

                //为指定的代码文档对象模型(CodeDOM) 编译单元生成代码并将其发送到指定的文本编写器，使用指定的选项。
                //将自定义代码编译器(代码内容)、和代码格式写入到streamWriter中
                provider.GenerateCodeFromCompileUnit(codeCompileUnit, streamWriter, options);
            }
        }

        public static void AddClass(DescriptorProto message, CodeCompileUnit codeCompileUnit, CodeNamespace myNamespace)
        {
            Dictionary<CodeMemberField, FieldDescriptorProto> fieldInfo = new Dictionary<CodeMemberField, FieldDescriptorProto>();

            //Code:代码体
            CodeTypeDeclaration myClass = new CodeTypeDeclaration(ToWrapperString(message.Name));
            //指定为类
            myClass.IsClass = true;
            //添加基类
            //myClass.BaseTypes.Add("");
            //设置类的访问类型
            myClass.TypeAttributes = TypeAttributes.Public;// | TypeAttributes.Sealed;
            //设置类序列化
            myClass.CustomAttributes.Add(new CodeAttributeDeclaration("System.Serializable"));
            //把这个类放在这个命名空间下
            myNamespace.Types.Add(myClass);

            //生成变量
            for (int i = 0; i < message.FieldList.Count; i++)
            {
                string tempStr = message.FieldList[i].TypeName;
                if (message.FieldList[i].Type.Equals(FieldDescriptorProto.Types.Type.TYPE_MESSAGE) ||
                    message.FieldList[i].Type.Equals(FieldDescriptorProto.Types.Type.TYPE_ENUM))
                {
                    if (tempStr.Split('.').Length == 3)
                    {
                        tempStr = tempStr.Split('.')[2];
                        if (message.FieldList[i].Type.Equals(FieldDescriptorProto.Types.Type.TYPE_MESSAGE))
                        {
                            tempStr = ToWrapperString(tempStr);
                        }
                        if (message.FieldList[i].Label.Equals(FieldDescriptorProto.Types.Label.LABEL_REPEATED))
                        {
                            tempStr = ToListString(tempStr);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Wrong Length in {message.Name} : {message.FieldList[i].TypeName}");
                    }
                }
                else
                {
                    try
                    {
                        tempStr = $"{typeMap[message.FieldList[i].Type]}";
                        if (message.FieldList[i].Label.Equals(FieldDescriptorProto.Types.Label.LABEL_REPEATED))
                        {
                            tempStr = ToListString(tempStr);
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"No Map in {message.FieldList[i].Type.ToString()}");
                    }
                }

                string name = GenerateCamelString(message.FieldList[i].Name.Split('_'));
                fieldInfo[AddPublicStringField(myClass, tempStr, name)] = message.FieldList[i];
            }

            ConstructMethod(myClass, message, fieldInfo);
            LoadBaseToWrapper(myClass, message, fieldInfo);
            OutputBaseFromWrapper(myClass, message, fieldInfo);

            //读取oneof
            if (message.UnknownFields[8].LengthDelimitedList.Count > 0)
            {
                foreach (var oneOfMessage in message.UnknownFields[8].LengthDelimitedList)
                {
                    string str = oneOfMessage.ToStringUtf8().Replace("\n", "");
                    str= str.Replace("\t", "");
                    string name = str.Substring(0, 1).ToUpper() + str.Substring(1);
                    string finalString = $"{message.Name}.{name}OneofCase";
                    AddPublicStringField(myClass, finalString, name);
                }
            }
        }

        private static void ConstructMethod(CodeTypeDeclaration myClass, DescriptorProto message, Dictionary<CodeMemberField, FieldDescriptorProto> fieldInfo)
        {
            //Add Construct Method
            CodeStatementCollection constructerStatements = new CodeStatementCollection();

            foreach (CodeTypeMember field in myClass.Members)
            {
                if (field is CodeMemberField)
                {
                    CodeMemberField curField = field as CodeMemberField;
                    FieldDescriptorProto fieldDescriptorProto = fieldInfo[curField];
                    var leftVariable = new CodeVariableReferenceExpression(curField.Name);
                    string rightVariableString;
                    if (fieldDescriptorProto.Label.Equals(FieldDescriptorProto.Types.Label.LABEL_REPEATED))
                    {
                        rightVariableString = $"new {curField.Type.BaseType}()";
                    }
                    else
                    {
                        rightVariableString = GenerateInitialString(fieldDescriptorProto, curField.Type.BaseType);
                    }
                    constructerStatements.Add(new CodeAssignStatement(leftVariable, new CodeSnippetExpression(rightVariableString)));
                }
            }

            //读取oneof
            if (message.UnknownFields[8].LengthDelimitedList.Count > 0)
            {
                foreach (var oneOfMessage in message.UnknownFields[8].LengthDelimitedList)
                {
                    string str = oneOfMessage.ToStringUtf8().Replace("\n", "");
                    str = str.Replace("\t", "");
                    string name = str.Substring(0, 1).ToUpper() + str.Substring(1);
                    string finalString = $"{message.Name}.{name}OneofCase";
                    constructerStatements.Add(new CodeAssignStatement(new CodeSnippetExpression(name), new CodeSnippetExpression($"({finalString})0")));
                }
            }

            AddCodeConstructor(myClass, ToWrapperString(message.Name), constructerStatements);
        }

        private static void LoadBaseToWrapper(CodeTypeDeclaration myClass, DescriptorProto message, Dictionary<CodeMemberField, FieldDescriptorProto> fieldInfo)
        {
            //Add LoadBaseToWrapper Method
            CodeStatementCollection loadBaseToWrapperStatements = new CodeStatementCollection();

            foreach (CodeTypeMember field in myClass.Members)
            {
                if (field is CodeMemberField)
                {
                    CodeMemberField curField = field as CodeMemberField;
                    FieldDescriptorProto fieldDescriptorProto = fieldInfo[curField];

                    var leftVariable = new CodeVariableReferenceExpression(curField.Name);
                    string rightVariableString = "";

                    if (fieldDescriptorProto.Label.Equals(FieldDescriptorProto.Types.Label.LABEL_REPEATED))
                    {
                        string tempClassStr = fieldDescriptorProto.TypeName;
                        loadBaseToWrapperStatements.Add(new CodeSnippetStatement($"\t\t\t{curField.Name}.Clear();"));
                        if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_MESSAGE) ||
                            fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_ENUM))
                        {
                            if (tempClassStr.Split('.').Length == 3)
                            {
                                tempClassStr = tempClassStr.Split('.')[2];
                                if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_MESSAGE))
                                {
                                    tempClassStr = ToWrapperString(tempClassStr);
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Wrong Length in {message.Name} : {fieldDescriptorProto.TypeName}");
                            }
                        }
                        else
                        {
                            try
                            {
                                tempClassStr = $"{typeMap[fieldDescriptorProto.Type]}";
                            }
                            catch (Exception)
                            {
                                Console.WriteLine($"No Map in {fieldDescriptorProto.Type.ToString()}");
                            }
                        }

                        string forVariableStr = "pos";
                        string tempVar = "tempVar";
                        string firstRightVariavleString;
                        CodeStatement firstStatement;
                        CodeStatement codeStatement;
                        CodeStatement lastStatement;

                        //get first
                        firstRightVariavleString = GenerateInitialString(fieldDescriptorProto, tempClassStr);
                        firstStatement = new CodeAssignStatement(new CodeVariableReferenceExpression($"{tempClassStr} {tempVar}"), new CodeSnippetExpression(firstRightVariavleString));

                        //get mid
                        if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_MESSAGE))
                        {

                            rightVariableString = $"{tempVar}.LoadBaseToWrapper({baseString}.{curField.Name}[{forVariableStr}]);";
                            codeStatement = new CodeSnippetStatement($"\t\t\t\t\t{rightVariableString}");
                        }
                        else
                        {
                            rightVariableString = $"{baseString}.{curField.Name}[{forVariableStr}]";
                            codeStatement = new CodeAssignStatement(new CodeVariableReferenceExpression(tempVar), new CodeVariableReferenceExpression(rightVariableString));
                        }

                        //get last
                        lastStatement = new CodeSnippetStatement($"\t\t\t\t\t{curField.Name}.Add({tempVar});");

                        //生成循环
                        // Creates a for loop that sets testInt to 0 and continues incrementing testInt by 1 each loop until testInt is not less than 10.
                        CodeIterationStatement forLoop = new CodeIterationStatement(
                        // initStatement parameter for pre-loop initialization.
                        new CodeVariableDeclarationStatement(typeof(int), forVariableStr, new CodePrimitiveExpression(0)),
                        // testExpression parameter to test for continuation condition.
                        new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression(forVariableStr),
                            CodeBinaryOperatorType.LessThan, new CodeSnippetExpression($"{baseString}.{curField.Name}.Count")),
                        // incrementStatement parameter indicates statement to execute after each iteration.
                        new CodeAssignStatement(new CodeVariableReferenceExpression(forVariableStr), new CodeBinaryOperatorExpression(
                            new CodeVariableReferenceExpression(forVariableStr), CodeBinaryOperatorType.Add, new CodePrimitiveExpression(1))),
                        // statements parameter contains the statements to execute during each interation of the loop.
                        new CodeStatement[] { firstStatement, codeStatement, lastStatement });
                        // Create a CodeConditionStatement that tests a boolean value named boolean.
                        CodeConditionStatement conditionalStatement = new CodeConditionStatement(
                            // The condition to test.
                            new CodeVariableReferenceExpression($"{baseString}.{curField.Name} != null"),
                            // The statements to execute if the condition evaluates to true.
                            new CodeStatement[] { forLoop });

                        loadBaseToWrapperStatements.Add(conditionalStatement);
                    }
                    else
                    {
                        CodeStatement codeStatement;
                        if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_MESSAGE))
                        {
                            rightVariableString = $"\t\t\t{curField.Name}.LoadBaseToWrapper({baseString}.{curField.Name});";
                            codeStatement = new CodeSnippetStatement(rightVariableString);
                            // Create a CodeConditionStatement that tests a boolean value named boolean.
                            codeStatement = new CodeConditionStatement(
                                // The condition to test.
                                new CodeVariableReferenceExpression($"{baseString}.{curField.Name} != null"),
                                // The statements to execute if the condition evaluates to true.
                                new CodeStatement[] { codeStatement });
                        }
                        else
                        {
                            rightVariableString = $"{baseString}.{curField.Name}";
                            codeStatement = new CodeAssignStatement(leftVariable, new CodeVariableReferenceExpression(rightVariableString));
                        }
                        loadBaseToWrapperStatements.Add(codeStatement);
                    }
                }
            }

            //读取oneof
            if (message.UnknownFields[8].LengthDelimitedList.Count > 0)
            {
                foreach (var oneOfMessage in message.UnknownFields[8].LengthDelimitedList)
                {
                    string str = oneOfMessage.ToStringUtf8().Replace("\n", "");
                    str = str.Replace("\t", "");
                    string name = str.Substring(0, 1).ToUpper() + str.Substring(1);
                    string finalString = $"{message.Name}.{name}OneofCase";
                    loadBaseToWrapperStatements.Add(new CodeAssignStatement(new CodeSnippetExpression(name), new CodeSnippetExpression($"{baseString}.{name}Case")));
                }
            }

            AddPublicMethod(myClass, "LoadBaseToWrapper", loadBaseToWrapperStatements, new CodeTypeReference(typeof(void)), new string[] { message.Name }, new string[] { baseString });
        }

        private static void OutputBaseFromWrapper(CodeTypeDeclaration myClass, DescriptorProto message, Dictionary<CodeMemberField, FieldDescriptorProto> fieldInfo)
        {
            //Add OutputBaseFromWrapper
            CodeStatementCollection outputBaseFromWrapperStatements = new CodeStatementCollection();

            outputBaseFromWrapperStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression($"{message.Name} { baseString }"), new CodeVariableReferenceExpression($"new {message.Name}()")));
            foreach (CodeTypeMember field in myClass.Members)
            {
                if (field is CodeMemberField)
                {
                    CodeMemberField curField = field as CodeMemberField;
                    FieldDescriptorProto fieldDescriptorProto = fieldInfo[curField];

                    var leftVariable = new CodeVariableReferenceExpression($"{baseString}.{curField.Name}");
                    string rightVariableString = "";

                    if (fieldDescriptorProto.Label.Equals(FieldDescriptorProto.Types.Label.LABEL_REPEATED))
                    {
                        string tempClassStr = fieldDescriptorProto.TypeName;
                        if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_MESSAGE) ||
                            fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_ENUM))
                        {
                            if (tempClassStr.Split('.').Length == 3)
                            {
                                tempClassStr = tempClassStr.Split('.')[2];
                            }
                            else
                            {
                                Console.WriteLine($"Wrong Length in {message.Name} : {fieldDescriptorProto.TypeName}");
                            }
                        }
                        else
                        {
                            try
                            {
                                tempClassStr = $"{typeMap[fieldDescriptorProto.Type]}";
                            }
                            catch (Exception)
                            {
                                Console.WriteLine($"No Map in {fieldDescriptorProto.Type.ToString()}");
                            }
                        }

                        string forVariableStr = "pos";
                        string tempVar = "tempVar";
                        string firstRightVariavleString = "";
                        CodeStatement firstStatement;
                        CodeStatement codeStatement;
                        CodeStatement lastStatement;

                        //get first
                        if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_MESSAGE))
                        {
                            firstRightVariavleString = $"new {tempClassStr}()";
                        }
                        else if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_ENUM))
                        {
                            firstRightVariavleString = $"({tempClassStr})0";
                        }
                        else if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_STRING))
                        {
                            firstRightVariavleString = $"\"\"";
                        }
                        else if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_BOOL))
                        {
                            firstRightVariavleString = $"false";
                        }
                        else
                        {
                            firstRightVariavleString = $"0";
                        }
                        firstStatement = new CodeAssignStatement(new CodeVariableReferenceExpression($"{tempClassStr} {tempVar}"), new CodeSnippetExpression(firstRightVariavleString));

                        //get mid
                        if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_MESSAGE))
                        {

                            rightVariableString = $"{curField.Name}[{forVariableStr}].OutputBaseFromWrapper()";
                        }
                        else
                        {
                            rightVariableString = $"{curField.Name}[{forVariableStr}]";
                        }
                        codeStatement = new CodeAssignStatement(new CodeVariableReferenceExpression(tempVar), new CodeVariableReferenceExpression(rightVariableString));

                        //get last
                        lastStatement = new CodeSnippetStatement($"\t\t\t\t{baseString}.{curField.Name}.Add({tempVar});");

                        // Creates a for loop that sets testInt to 0 and continues incrementing testInt by 1 each loop until testInt is not less than 10.
                        CodeIterationStatement forLoop = new CodeIterationStatement(
                        // initStatement parameter for pre-loop initialization.
                        new CodeVariableDeclarationStatement(typeof(int), forVariableStr, new CodePrimitiveExpression(0)),
                        // testExpression parameter to test for continuation condition.
                        new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression(forVariableStr),
                            CodeBinaryOperatorType.LessThan, new CodeSnippetExpression($"{curField.Name}.Count")),
                        // incrementStatement parameter indicates statement to execute after each iteration.
                        new CodeAssignStatement(new CodeVariableReferenceExpression(forVariableStr), new CodeBinaryOperatorExpression(
                            new CodeVariableReferenceExpression(forVariableStr), CodeBinaryOperatorType.Add, new CodePrimitiveExpression(1))),
                        // statements parameter contains the statements to execute during each interation of the loop.
                        new CodeStatement[] { firstStatement, codeStatement, lastStatement });

                        outputBaseFromWrapperStatements.Add(forLoop);
                    }
                    else
                    {
                        CodeStatement codeStatement;
                        if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_MESSAGE))
                        {
                            rightVariableString = $"{curField.Name}.OutputBaseFromWrapper()";
                        }
                        else
                        {
                            rightVariableString = $"{curField.Name}";
                        }
                        codeStatement = new CodeAssignStatement(leftVariable, new CodeVariableReferenceExpression(rightVariableString));

                        outputBaseFromWrapperStatements.Add(codeStatement);
                    }
                }
            }

            //读取oneof
            if (message.UnknownFields[8].LengthDelimitedList.Count > 0)
            {
                foreach (var oneOfMessage in message.UnknownFields[8].LengthDelimitedList)
                {
                    string str = oneOfMessage.ToStringUtf8().Replace("\n", "");
                    str = str.Replace("\t", "");
                    string name = str.Substring(0, 1).ToUpper() + str.Substring(1);
                    string className = $"{message.Name}.{name}OneofCase";
                    string finalString = "";
                    foreach (CodeTypeMember field in myClass.Members)
                    {
                        if (field is CodeMemberField)
                        {
                            CodeMemberField curField = field as CodeMemberField;
                            FieldDescriptorProto fieldDescriptorProto = fieldInfo[curField];

                            var leftVariable = $"{baseString}.{curField.Name}";
                            string rightVariableString = "";

                            if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_MESSAGE))
                            {
                                rightVariableString = $"{curField.Name}.OutputBaseFromWrapper()";
                            }
                            else
                            {
                                rightVariableString = $"{curField.Name}";
                            }
                            finalString += $@"
                case {className}.{curField.Name}:
                    {leftVariable} = {rightVariableString};
                    break;";
                        }
                    }
                    outputBaseFromWrapperStatements.Add(new CodeSnippetStatement($@"
            {baseString}.Clear{name}();
            switch({name})
            {{{finalString}
            }}"));
                }
            }

            outputBaseFromWrapperStatements.Add(new CodeVariableReferenceExpression($"return { baseString }"));

            AddPublicMethod(myClass, "OutputBaseFromWrapper", outputBaseFromWrapperStatements, new CodeTypeReference(message.Name));
        }

        private static string GenerateCamelString(string[] stringList)
        {
            string name = "";
            foreach (var str in stringList)
            {
                name += str.Substring(0, 1).ToUpper() + str.Substring(1);
            }
            return name;
        }

        private static string GenerateInitialString(FieldDescriptorProto fieldDescriptorProto,string tempString)
        {
            string rightVariableString;
            if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_MESSAGE))
            {
                rightVariableString = $"new {tempString}()";
            }
            else if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_ENUM))
            {
                rightVariableString = $"({tempString})0";
            }
            else if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_STRING))
            {
                rightVariableString = $"\"\"";
            }
            else if (fieldDescriptorProto.Type.Equals(FieldDescriptorProto.Types.Type.TYPE_BOOL))
            {
                rightVariableString = $"false";
            }
            else
            {
                rightVariableString = $"0";
            }
            return rightVariableString;
        }

        private static string ToWrapperString(string str)
        {
            return $"{str}Wrapper";
        }

        private static string ToListString(string str)
        {
            return $"List<{str}>";
        }

        private static CodeMemberField AddPublicStringField(CodeTypeDeclaration myClass, string type, string variableName)
        {
            //添加字段
            CodeMemberField field = new CodeMemberField(type, variableName);
            //设置访问类型
            field.Attributes = MemberAttributes.Public;
            //添加到myClass类中
            myClass.Members.Add(field);
            return field;
        }

        private static void AddCodeConstructor(CodeTypeDeclaration myClass, string methodName, CodeStatementCollection statements)
        {
            //添加方法
            CodeConstructor method = new CodeConstructor();
            //方法名
            method.Name = methodName;
            //访问类型
            method.Attributes = MemberAttributes.Public;// | MemberAttributes.Final

            //设置返回值类型：int/不设置则为void
            method.ReturnType = null;
            //设置内部内容
            foreach (CodeStatement statement in statements)
            {
                method.Statements.Add(statement);
            }
            //将方法添加到myClass类中
            myClass.Members.Add(method);
        }

        private static void AddPublicMethod(CodeTypeDeclaration myClass, string methodName, CodeStatementCollection statements, CodeTypeReference returnType = null, string[] parametersType = null, string[] parametersName = null)
        {
            if (returnType == null)
            {
                returnType = new CodeTypeReference(typeof(void));
            }
            //添加方法
            CodeMemberMethod method = new CodeMemberMethod();
            //方法名
            method.Name = methodName;
            //访问类型
            method.Attributes = MemberAttributes.Public;// | MemberAttributes.Final
            if (parametersName != null && parametersType != null && parametersName.Length == parametersType.Length)
            {
                //添加参数
                for (int pos = 0; pos < parametersName.Length; pos++)
                {
                    method.Parameters.Add(new CodeParameterDeclarationExpression(parametersType[pos], parametersName[pos]));
                }
            }
            //设置返回值类型：int/不设置则为void
            method.ReturnType = returnType;
            //设置内部内容
            foreach (CodeStatement statement in statements)
            {
                method.Statements.Add(statement);
            }
            //将方法添加到myClass类中
            myClass.Members.Add(method);
        }

        static void Main(string[] args)
        {
            if (args.Length == 2)
            {
                Program.ParsePB(args[0], args[1]);
            }
            else
            {
                Console.WriteLine("wrong input pls input two args");
            }
            //Program.ParsePB("F:/CSOut.pb", "F:/test");
            Console.WriteLine("Parse End");
        }
    }
}
