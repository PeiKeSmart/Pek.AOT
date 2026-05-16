using System.Reflection;

namespace NewLife.Reflection;

class OrcasNamer
{
    public static String? GetName(MemberInfo member)
    {
        using TextWriter writer = new StringWriter();
        switch (member.MemberType)
        {
            case MemberTypes.TypeInfo:
            case MemberTypes.NestedType:
                writer.Write("T:");
                if (member is Type type) WriteType(type, writer);
                break;
            case MemberTypes.Field:
                writer.Write("F:");
                if (member is FieldInfo field) WriteField(field, writer);
                break;
            case MemberTypes.Property:
                writer.Write("P:");
                if (member is PropertyInfo property) WriteProperty(property, writer);
                break;
            case MemberTypes.Method:
                writer.Write("M:");
                if (member is MethodInfo method) WriteMethod(method, writer);
                break;
            case MemberTypes.Constructor:
                writer.Write("M:");
                if (member is ConstructorInfo constructor)
                {
                    if (!constructor.IsStatic)
                        WriteConstructor(constructor, writer);
                    else
                        WriteStaticConstructor(constructor, writer);
                }
                break;
            case MemberTypes.Event:
                writer.Write("E:");
                if (member is EventInfo trigger) WriteEvent(trigger, writer);
                break;
        }

        return writer.ToString();
    }

    private static void WriteEvent(EventInfo trigger, TextWriter writer)
    {
        WriteType(trigger.DeclaringType, writer);
        writer.Write(".{0}", trigger.Name);
    }

    private static void WriteField(FieldInfo field, TextWriter writer)
    {
        WriteType(field.DeclaringType, writer);
        writer.Write(".{0}", field.Name);
    }

    private static void WriteMethod(MethodInfo method, TextWriter writer)
    {
        var name = method.Name;
        WriteType(method.DeclaringType, writer);
        writer.Write(".{0}", name);

        if (method.IsGenericMethod)
        {
            var genericParameters = method.GetGenericArguments();
            if (genericParameters != null)
            {
                writer.Write("``{0}", genericParameters.Length);
            }
        }

        WriteParameters(method.GetParameters(), writer);
        if (name is "op_Implicit" or "op_Explicit")
        {
            writer.Write("~");
            WriteType(method.ReturnType, writer);
        }
    }

    private static void WriteConstructor(ConstructorInfo constructor, TextWriter writer)
    {
        WriteType(constructor.DeclaringType, writer);
        writer.Write(".#ctor");
        WriteParameters(constructor.GetParameters(), writer);
    }

    private static void WriteStaticConstructor(ConstructorInfo constructor, TextWriter writer)
    {
        WriteType(constructor.DeclaringType, writer);
        writer.Write(".#cctor");
        WriteParameters(constructor.GetParameters(), writer);
    }

    private static void WriteProperty(PropertyInfo property, TextWriter writer)
    {
        WriteType(property.DeclaringType, writer);
        writer.Write(".{0}", property.Name);
        WriteParameters(property.GetIndexParameters(), writer);
    }

    private static void WriteParameters(ParameterInfo[] parameters, TextWriter writer)
    {
        if (parameters == null || parameters.Length == 0) return;

        writer.Write("(");
        for (var index = 0; index < parameters.Length; index++)
        {
            if (index > 0) writer.Write(",");
            WriteType(parameters[index].ParameterType, writer);
        }
        writer.Write(")");
    }

    private static void WriteType(Type? type, TextWriter writer)
    {
        if (type == null) return;

        if (type.IsArray)
        {
            WriteType(type.GetElementType(), writer);
            writer.Write("[");
            if (type.GetArrayRank() > 1)
            {
                for (var index = 0; index < type.GetArrayRank(); index++)
                {
                    if (index > 0) writer.Write(",");
                    writer.Write("0:");
                }
            }
            writer.Write("]");
        }
        else if (type.IsByRef)
        {
            WriteType(type.GetElementType(), writer);
            writer.Write("@");
        }
        else if (type.IsPointer)
        {
            WriteType(type.GetElementType(), writer);
            writer.Write("*");
        }
        else
        {
            if (type.IsGenericParameter)
            {
                if (type.DeclaringMethod != null)
                    writer.Write("``");
                else if (type.DeclaringType != null)
                    writer.Write("`");
                else
                    throw new InvalidOperationException("Generic parameter not on type or method.");

                writer.Write(type.GenericParameterPosition);
            }
            else
            {
                var declaringType = type.DeclaringType;
                if (declaringType != null)
                {
                    WriteType(declaringType, writer);
                    writer.Write(".");
                }
                else if (!String.IsNullOrEmpty(type.Namespace))
                {
                    writer.Write(type.Namespace);
                    writer.Write(".");
                }

                var typeName = type.Name;
                var position = typeName.IndexOf('`');
                if (position >= 0)
                    writer.Write(typeName[..position]);
                else
                    writer.Write(typeName);

                if (type.IsGenericType)
                {
                    if (type.IsGenericTypeDefinition)
                    {
                        var parameters = type.GetGenericArguments();
                        if (parameters != null)
                        {
                            writer.Write("`{0}", parameters.Length);
                        }
                    }
                    else
                    {
                        var arguments = type.GetGenericArguments();
                        if (arguments != null && arguments.Length > 0)
                        {
                            writer.Write("{");
                            for (var index = 0; index < arguments.Length; index++)
                            {
                                if (index > 0) writer.Write(",");
                                WriteType(arguments[index], writer);
                            }
                            writer.Write("}");
                        }
                    }
                }
            }
        }
    }
}