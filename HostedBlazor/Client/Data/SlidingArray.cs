using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HostedBlazor.Data
{
    public class SlidingArray
    {
        private int length = 5;
        public double[] array = new double[5];
        private int index = 0;
        public double average {
            get { return array.Average(); } 
        }

        public SlidingArray(int length )
        {
            this.length = length;
            this.array = new double[length];
            this.index = 0;
        }

        public void AddEta(double eta)
        {
            array[index] = eta;
            index = (index + 1) % 5;
        }
    }
}
