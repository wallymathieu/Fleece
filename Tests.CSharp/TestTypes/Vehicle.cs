namespace Tests.CSharp.TestTypes
{
    public abstract class Vehicle
    {
        public class Bike : Vehicle
        {
        }
        public class MotorBike : Vehicle
        {
        }
        public class Car : Vehicle
        {
            public Car(string make)
            {
                Make = make;
            }

            public string Make { get;  }
        }
        public class Van : Vehicle
        {
            public Van(string make, float capacity)
            {
                Make = make;
                Capacity = capacity;
            }

            public string Make { get;  }
            public float Capacity { get; }
        }
        public class Truck : Vehicle
        {
            public Truck(string make, float capacity)
            {
                Make = make;
                Capacity = capacity;
            }

            public string Make { get;  }
            public float Capacity { get; }
        }
        public class Aircraft : Vehicle
        {
            public Aircraft(string make, float capacity)
            {
                Make = make;
                Capacity = capacity;
            }

            public string Make { get;  }
            public float Capacity { get; }
        }
        
    }
}