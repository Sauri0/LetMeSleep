using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

// Field-only JSON writer. Handles external diagnostic DTOs without Unity serialization.
// Does not visit properties (Vector3.normalized would recurse); no Unity native calls.
public static class AuthorityStrikeJson
{
    public static string Write(object value) { var b=new StringBuilder();Append(b,value);return b.ToString(); }
    static void Append(StringBuilder b,object value)
    {
        if(value==null){b.Append("null");return;}
        if(value is string text){Quote(b,text);return;}
        if(value is bool flag){b.Append(flag?"true":"false");return;}
        var type=value.GetType();
        if(type.IsEnum){Quote(b,value.ToString());return;}
        if(value is float f){if(float.IsNaN(f)||float.IsInfinity(f))throw new InvalidOperationException("Nonfinite float in probe receipt");b.Append(f.ToString("R",CultureInfo.InvariantCulture));return;}
        if(value is double d){if(double.IsNaN(d)||double.IsInfinity(d))throw new InvalidOperationException("Nonfinite double in probe receipt");b.Append(d.ToString("R",CultureInfo.InvariantCulture));return;}
        if(type.IsPrimitive || value is decimal){b.Append(Convert.ToString(value,CultureInfo.InvariantCulture));return;}
        if(value is IEnumerable list)
        {
            b.Append('[');bool first=true;
            foreach(var item in list){if(!first)b.Append(',');first=false;Append(b,item);}b.Append(']');return;
        }
        b.Append('{');bool start=true;
        foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance))
        {if(!start)b.Append(',');start=false;Quote(b,field.Name);b.Append(':');Append(b,field.GetValue(value));}
        b.Append('}');
    }
    static void Quote(StringBuilder b,string text)
    {
        b.Append('"');
        foreach(char c in text)
        {
            if(c=='"')b.Append("\\\"");else if(c=='\\')b.Append("\\\\");
            else if(c<32 || char.IsSurrogate(c))b.Append("\\u").Append(((int)c).ToString("x4"));
            else b.Append(c);
        }
        b.Append('"');
    }
}

