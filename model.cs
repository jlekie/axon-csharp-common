public interface IModel
{
    string GetModelName();
    string[] GetPropertyNames();
    string[] GetDefinedPropertyNames();
    object GetPropertyValue(string propertyName);
    bool IsPropertyDefined(string propertyName);
}

