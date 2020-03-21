using System.Collections.Generic;

namespace Tests.CSharp.TestTypes
{
    public struct Person {
        public Person(string name, int age, Gender gender, IReadOnlyList<Person> children)
        {
            Name = name;
            Age = age;
            Gender = gender;
            Children = children;
        }

        public string Name {get;}
        public int Age{get;}
        public Gender Gender{get;}
        IReadOnlyList<Person> Children{get;}
    }
}