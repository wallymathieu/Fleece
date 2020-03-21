namespace Tests.CSharp.TestTypes
{
    public struct Attribute {
        public Attribute(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; } 
        public string Value{ get; }
    }
}