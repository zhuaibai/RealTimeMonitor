using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealTimeMonitor.Model
{
    public class DataService
    {
        private readonly Random _random = new Random();
        private double _currentValue = 10;
        private const double MaxDeviation = 2.0;

        public double GetNextDataPoint()
        {
            _currentValue += (_random.NextDouble() - 0.5) * MaxDeviation;

            // 限制在0-20范围内
            if (_currentValue < 0) _currentValue = 0;
            if (_currentValue > 20) _currentValue = 20;

            return _currentValue;
        }
    }
}
