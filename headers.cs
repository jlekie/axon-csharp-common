using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Axon
{
    public struct RequestHeader
    {
        public readonly string ActionName;
        public readonly int ArgumentCount;
        
        public RequestHeader(string actionName, int argumentCount)
        {
            this.ActionName = actionName;
            this.ArgumentCount = argumentCount;
        }
    }

    public struct RequestArgumentHeader
    {
        public readonly string ArgumentName;
        public readonly string Type;
        
        public RequestArgumentHeader(string argumentName, string type)
        {
            this.ArgumentName = argumentName;
            this.Type = type;
        }
    }

    public struct ResponseHeader
    {
        public readonly bool Success;

        public ResponseHeader(bool success)
        {
            this.Success = success;
        }
    }

    public struct ModelHeader
    {
        public readonly string ModelName;
        public readonly int PropertyCount;

        public ModelHeader(string modelName, int propertyCount)
        {
            this.ModelName = modelName;
            this.PropertyCount = propertyCount;
        }
    }

    public struct ModelPropertyHeader
    {
        public readonly string PropertyName;
        public readonly string Type;

        public ModelPropertyHeader(string propertyName, string type)
        {
            this.PropertyName = propertyName;
            this.Type = type;
        }
    }

    public struct ArrayHeader
    {
        public readonly int ItemCount;

        public ArrayHeader(int itemCount)
        {
            this.ItemCount = itemCount;
        }
    }

    public struct ArrayItemHeader
    {
        public readonly string Type;

        public ArrayItemHeader(string type)
        {
            this.Type = type;
        }
    }
}