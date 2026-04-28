namespace NarrativeTool.Core.Runtime
{
    public interface IVariableAccess
    {
        object GetValue(string name);
        void SetValue(string name, object value);
    }
}