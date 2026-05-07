using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CafeSimulation
{
    #region Вспомогательные классы

    enum EventType
    {
        GroupArrival,
        CustomerArrival,
        ServiceComplete
    }

    class Event : IComparable<Event>
    {
        public double Time { get; set; }
        public EventType Type { get; set; }
        public Customer Customer { get; set; }
        public string StageName { get; set; }
        public int ChannelIndex { get; set; } = -1;

        public int CompareTo(Event other)
        {
            return Time.CompareTo(other.Time);
        }
    }

    class Customer
    {
        public int Id { get; set; }
        public double EntryTime { get; set; }
        public double ExitTime { get; set; }
        public double TotalWaitTime { get; set; }
        public bool HasTray { get; set; }

        public bool WantFirst { get; set; }
        public bool WantSecond { get; set; }
        public bool WantJuice { get; set; }
        public bool WantBread { get; set; }
        public bool Disturbance { get; set; }

        public bool WantSalad { get; set; }
        public bool RejectedFirst { get; set; }
        public bool RejectedDrink { get; set; }
        public bool HasAnyPaidItem { get; set; }

        public double FirstStartWait { get; set; }
        public double SecondStartWait { get; set; }
        public double DrinkStartWait { get; set; }
        public double BreadStartWait { get; set; }
        public double CutleryStartWait { get; set; }
        public double PaymentStartWait { get; set; }

        public double FirstServiceTime { get; set; }
        public double SecondServiceTime { get; set; }
        public double DrinkServiceTime { get; set; }
        public double BreadServiceTime { get; set; }
        public double CutleryServiceTime { get; set; }
        public double PaymentServiceTime { get; set; }
    }

    class ServiceStage
    {
        public string Name { get; set; }
        public Queue<Customer> Queue { get; set; } = new Queue<Customer>();
        public bool IsBusy { get; set; }
        public double BusyTime { get; set; }
        public int ServedCount { get; set; }
        public int BypassedCount { get; set; }
        public int MaxQueueLength { get; set; }
        public double TotalWaitTime { get; set; }

        public void UpdateQueueStats()
        {
            MaxQueueLength = Math.Max(MaxQueueLength, Queue.Count);
        }
    }

    class MultiChannelStage
    {
        public string Name { get; set; }
        public int ChannelCount { get; set; }
        public bool[] IsBusy { get; set; }
        public Queue<Customer> Queue { get; set; } = new Queue<Customer>();
        public double[] BusyTime { get; set; }
        public int ServedCount { get; set; }
        public int BypassedCount { get; set; }
        public int MaxQueueLength { get; set; }
        public double TotalWaitTime { get; set; }

        public MultiChannelStage(string name, int channelCount)
        {
            Name = name;
            ChannelCount = channelCount;
            IsBusy = new bool[channelCount];
            BusyTime = new double[channelCount];
        }

        public void UpdateQueueStats()
        {
            MaxQueueLength = Math.Max(MaxQueueLength, Queue.Count);
        }

        public int GetFreeChannel()
        {
            for (int i = 0; i < ChannelCount; i++)
                if (!IsBusy[i]) return i;
            return -1;
        }
    }

    class Fragment1
    {
        public string Name { get; set; }
        public ServiceStage Channel { get; set; } = new ServiceStage();
        public Queue<Customer> OutputBuffer { get; set; } = new Queue<Customer>();
        public int MaxBufferSize { get; set; }
        public double Probability { get; set; }
        public double MinTime { get; set; }
        public double MaxTime { get; set; }

        public Fragment1(string name, double prob, double minTime, double maxTime, int bufferSize)
        {
            Name = name;
            Probability = prob;
            MinTime = minTime;
            MaxTime = maxTime;
            MaxBufferSize = bufferSize;
        }

        public bool IsValveOpen()
        {
            return !Channel.IsBusy && OutputBuffer.Count < MaxBufferSize;
        }
    }

    class Fragment4
    {
        public string Name { get; set; }
        public Queue<Customer> Buffer { get; set; } = new Queue<Customer>();
        public ServiceStage Channel { get; set; } = new ServiceStage();
        public int MaxBufferSize { get; set; }
        public double Probability { get; set; }
        public double MinTime { get; set; }
        public double MaxTime { get; set; }
        public int RejectCount { get; set; }

        public Fragment4(string name, double prob, double minTime, double maxTime, int maxBufferSize)
        {
            Name = name;
            Probability = prob;
            MinTime = minTime;
            MaxTime = maxTime;
            MaxBufferSize = maxBufferSize;
        }

        public bool IsValveOpen()
        {
            return Buffer.Count < MaxBufferSize;
        }
    }

    #endregion

    class CafeSimulation
    {
        private Random _rand;
        private double _currentTime;
        private PriorityQueue<Event, double> _eventQueue;
        private int _nextCustomerId;

        private const double SIMULATION_TIME = 540;  // 9 часов = 540 минут
        private const int TOTAL_TRAYS = 30;

        private int _totalCustomers;
        private int _servedCustomers;
        private double _totalSystemTime;
        private double _totalWaitTime;

        private int _freeTrays;
        private int _activeCustomers;

        private Fragment1 _firstDishStage1;
        private Fragment1 _secondDishStage1;
        private MultiChannelStage _drinkStage1;
        private MultiChannelStage _breadStage1;
        private ServiceStage _cutleryStage1;
        private ServiceStage _paymentStage1;

        private Fragment4 _firstDishStage2;
        private MultiChannelStage _secondDishStage2;
        private MultiChannelStage _saladStage2;
        private ServiceStage _paymentStage2;
        private Fragment4 _drinkStage2;

        public int RejectFirstMeal { get; private set; }
        public int RejectDrink { get; private set; }

        private int _firstDishTakenV1;
        private int _secondDishTakenV1;
        private int _juiceTakenV1;
        private int _teaTakenV1;
        private int _breadTakenV1;
        private int _disturbanceCountV1;
        private int _noServiceNeededV1;
        private double _totalServiceTimeV1;
        private double _totalMealTimeV1;

        private int _firstDishTakenV2;
        private int _secondDishTakenV2;
        private int _saladTakenV2;
        private int _drinkTakenV2;
        private int _noServiceNeededV2;
        private double _totalServiceTimeV2;
        private double _totalMealTimeV2;

        public double AvgWaitTime { get; private set; }
        public double AvgSystemTime { get; private set; }
        public double AvgServiceTime { get; private set; }
        public double AvgMealTime { get; private set; }
        public int TotalCustomers { get; private set; }
        public int FirstDishTaken { get; private set; }
        public int SecondDishTaken { get; private set; }
        public int JuiceTaken { get; private set; }
        public int TeaTaken { get; private set; }
        public int BreadTaken { get; private set; }
        public int DisturbanceCount { get; private set; }
        public int NoServiceNeeded { get; private set; }
        public int SaladTaken { get; private set; }
        public int DrinkTaken { get; private set; }
        public int RejectFirstMealResult { get; private set; }
        public int RejectDrinkResult { get; private set; }
        public double RejectFirstMealPercent { get; private set; }
        public double RejectDrinkPercent { get; private set; }

        public CafeSimulation()
        {
            _rand = new Random();
            _eventQueue = new PriorityQueue<Event, double>();
            _nextCustomerId = 1;
            _freeTrays = TOTAL_TRAYS;
            _activeCustomers = 0;
            _currentTime = 0;
        }

        private void ResetDay()
        {
            _currentTime = 0;
            _eventQueue.Clear();
            _freeTrays = TOTAL_TRAYS;
            _activeCustomers = 0;
            _nextCustomerId = 1;

            _firstDishTakenV1 = 0;
            _secondDishTakenV1 = 0;
            _juiceTakenV1 = 0;
            _teaTakenV1 = 0;
            _breadTakenV1 = 0;
            _disturbanceCountV1 = 0;
            _noServiceNeededV1 = 0;
            _totalServiceTimeV1 = 0;
            _totalMealTimeV1 = 0;

            _firstDishTakenV2 = 0;
            _secondDishTakenV2 = 0;
            _saladTakenV2 = 0;
            _drinkTakenV2 = 0;
            _noServiceNeededV2 = 0;
            _totalServiceTimeV2 = 0;
            _totalMealTimeV2 = 0;

            _totalCustomers = 0;
            _servedCustomers = 0;
            _totalSystemTime = 0;
            _totalWaitTime = 0;

            RejectFirstMeal = 0;
            RejectDrink = 0;
        }

        #region Инициализация

        private void InitVariant1()
        {
            _firstDishStage1 = new Fragment1("Первое блюдо", 0.5, 1.0, 2.0, 3);
            _secondDishStage1 = new Fragment1("Второе блюдо", 0.8, 2.0, 2.5, 3);
            _drinkStage1 = new MultiChannelStage("Напитки", 3);
            _breadStage1 = new MultiChannelStage("Хлебобулочные", 3);
            _cutleryStage1 = new ServiceStage { Name = "Столовые приборы" };
            _paymentStage1 = new ServiceStage { Name = "Оплата" };
        }

        private void InitVariant2()
        {
            _firstDishStage2 = new Fragment4("Первое блюдо", 0.5, 1.0, 2.0, 4);
            _secondDishStage2 = new MultiChannelStage("Второе блюдо", 3);
            _saladStage2 = new MultiChannelStage("Салат", 3);
            _paymentStage2 = new ServiceStage { Name = "Оплата" };
            _drinkStage2 = new Fragment4("Напиток", 1.0, 1.0, 2.0, 4);
        }

        #endregion

        #region Генераторы

        private double Exponential(double mean)
        {
            return -mean * Math.Log(1.0 - _rand.NextDouble());
        }

        private double Uniform(double min, double max)
        {
            return min + _rand.NextDouble() * (max - min);
        }

        private double Normal(double mean, double stdDev)
        {
            double u1 = 1.0 - _rand.NextDouble();
            double u2 = 1.0 - _rand.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return Math.Max(0.1, mean + stdDev * randStdNormal);
        }

        private int GetGroupSize()
        {
            return _rand.Next(2, 6);
        }

        #endregion

        #region Вариант 1

        private Customer CreateCustomerV1()
        {
            var customer = new Customer { Id = _nextCustomerId++ };
            customer.WantFirst = _rand.NextDouble() < 0.5;
            customer.WantSecond = _rand.NextDouble() < 0.8;
            customer.WantJuice = _rand.NextDouble() < 0.3;
            customer.Disturbance = customer.WantJuice && (_rand.NextDouble() < 0.1);
            customer.WantBread = _rand.NextDouble() < 0.7;
            return customer;
        }

        private bool CanStartNewCustomer()
        {
            return _freeTrays > 0;
        }

        private bool IsSimulationComplete()
        {
            return _currentTime >= SIMULATION_TIME;
        }

        private void ProcessArrivalV1(Event evt)
        {
            if (IsSimulationComplete()) return;

            if (evt.Type == EventType.GroupArrival)
            {
                double nextInterval = Exponential(3.0);
                if (_currentTime + nextInterval < SIMULATION_TIME)
                    ScheduleEvent(EventType.GroupArrival, _currentTime + nextInterval, null);

                int groupSize = GetGroupSize();
                for (int i = 0; i < groupSize; i++)
                {
                    var customer = CreateCustomerV1();
                    ScheduleEvent(EventType.CustomerArrival, _currentTime, customer);
                }
            }
            else if (evt.Type == EventType.CustomerArrival)
            {
                if (!CanStartNewCustomer()) return;

                var customer = evt.Customer;
                _totalCustomers++;
                customer.EntryTime = _currentTime;
                customer.HasTray = true;
                _activeCustomers++;
                _freeTrays--;

                ProcessFirstDishV1(customer);
            }
        }

        private void ProcessFirstDishV1(Customer customer)
        {
            customer.FirstStartWait = _currentTime;

            if (customer.WantFirst)
            {
                _firstDishStage1.Channel.Queue.Enqueue(customer);
                _firstDishStage1.Channel.UpdateQueueStats();

                if (_firstDishStage1.IsValveOpen())
                    StartFirstDishServiceV1();
            }
            else
            {
                _firstDishStage1.OutputBuffer.Enqueue(customer);
                _firstDishStage1.Channel.BypassedCount++;
                ProcessOutputBufferV1(_firstDishStage1, ProcessSecondDishV1);
            }
        }

        private void StartFirstDishServiceV1()
        {
            if (_firstDishStage1.Channel.Queue.Count == 0) return;

            var customer = _firstDishStage1.Channel.Queue.Dequeue();
            _firstDishStage1.Channel.IsBusy = true;

            double serviceTime = Uniform(_firstDishStage1.MinTime, _firstDishStage1.MaxTime);
            customer.FirstServiceTime = serviceTime;
            double waitTime = _currentTime - customer.FirstStartWait;
            _firstDishStage1.Channel.TotalWaitTime += waitTime;
            customer.TotalWaitTime += waitTime;

            ScheduleEvent(EventType.ServiceComplete, _currentTime + serviceTime, customer, _firstDishStage1.Name);
        }

        private void ProcessSecondDishV1(Customer customer)
        {
            customer.SecondStartWait = _currentTime;

            if (customer.WantSecond)
            {
                _secondDishStage1.Channel.Queue.Enqueue(customer);
                _secondDishStage1.Channel.UpdateQueueStats();

                if (_secondDishStage1.IsValveOpen())
                    StartSecondDishServiceV1();
            }
            else
            {
                _secondDishStage1.OutputBuffer.Enqueue(customer);
                _secondDishStage1.Channel.BypassedCount++;
                ProcessOutputBufferV1(_secondDishStage1, ProcessDrinkV1);
            }
        }

        private void StartSecondDishServiceV1()
        {
            if (_secondDishStage1.Channel.Queue.Count == 0) return;

            var customer = _secondDishStage1.Channel.Queue.Dequeue();
            _secondDishStage1.Channel.IsBusy = true;

            double serviceTime = Uniform(_secondDishStage1.MinTime, _secondDishStage1.MaxTime);
            customer.SecondServiceTime = serviceTime;
            double waitTime = _currentTime - customer.SecondStartWait;
            _secondDishStage1.Channel.TotalWaitTime += waitTime;
            customer.TotalWaitTime += waitTime;

            ScheduleEvent(EventType.ServiceComplete, _currentTime + serviceTime, customer, _secondDishStage1.Name);
        }

        private void ProcessDrinkV1(Customer customer)
        {
            customer.DrinkStartWait = _currentTime;

            int freeChannel = _drinkStage1.GetFreeChannel();
            if (freeChannel >= 0)
                StartDrinkServiceV1(customer, freeChannel);
            else
            {
                _drinkStage1.Queue.Enqueue(customer);
                _drinkStage1.UpdateQueueStats();
            }
        }

        private void StartDrinkServiceV1(Customer customer, int channel)
        {
            _drinkStage1.IsBusy[channel] = true;

            double serviceTime = Uniform(1.0, 2.0);
            if (customer.Disturbance)
                serviceTime += 1.0;
            customer.DrinkServiceTime = serviceTime;

            double waitTime = _currentTime - customer.DrinkStartWait;
            _drinkStage1.TotalWaitTime += waitTime;
            customer.TotalWaitTime += waitTime;

            ScheduleEvent(EventType.ServiceComplete, _currentTime + serviceTime, customer, _drinkStage1.Name, channel);
        }

        private void ProcessBreadV1(Customer customer)
        {
            customer.BreadStartWait = _currentTime;

            if (!customer.WantBread)
            {
                _breadStage1.BypassedCount++;
                ProcessCutleryV1(customer);
                return;
            }

            int freeChannel = _breadStage1.GetFreeChannel();
            if (freeChannel >= 0)
                StartBreadServiceV1(customer, freeChannel);
            else
            {
                _breadStage1.Queue.Enqueue(customer);
                _breadStage1.UpdateQueueStats();
            }
        }

        private void StartBreadServiceV1(Customer customer, int channel)
        {
            _breadStage1.IsBusy[channel] = true;

            double serviceTime = Uniform(1.0, 2.0);
            customer.BreadServiceTime = serviceTime;
            double waitTime = _currentTime - customer.BreadStartWait;
            _breadStage1.TotalWaitTime += waitTime;
            customer.TotalWaitTime += waitTime;

            ScheduleEvent(EventType.ServiceComplete, _currentTime + serviceTime, customer, _breadStage1.Name, channel);
        }

        private void ProcessCutleryV1(Customer customer)
        {
            customer.CutleryStartWait = _currentTime;

            if (!_cutleryStage1.IsBusy)
                StartCutleryServiceV1(customer);
            else
            {
                _cutleryStage1.Queue.Enqueue(customer);
                _cutleryStage1.UpdateQueueStats();
            }
        }

        private void StartCutleryServiceV1(Customer customer)
        {
            _cutleryStage1.IsBusy = true;

            double serviceTime = Normal(1.0, 0.5);
            customer.CutleryServiceTime = serviceTime;
            double waitTime = _currentTime - customer.CutleryStartWait;
            _cutleryStage1.TotalWaitTime += waitTime;
            customer.TotalWaitTime += waitTime;

            ScheduleEvent(EventType.ServiceComplete, _currentTime + serviceTime, customer, _cutleryStage1.Name);
        }

        private void ProcessPaymentV1(Customer customer)
        {
            customer.PaymentStartWait = _currentTime;

            if (!_paymentStage1.IsBusy)
                StartPaymentServiceV1(customer);
            else
            {
                _paymentStage1.Queue.Enqueue(customer);
                _paymentStage1.UpdateQueueStats();
            }
        }

        private void StartPaymentServiceV1(Customer customer)
        {
            _paymentStage1.IsBusy = true;

            double serviceTime = Uniform(2.0, 3.0);
            customer.PaymentServiceTime = serviceTime;
            double waitTime = _currentTime - customer.PaymentStartWait;
            _paymentStage1.TotalWaitTime += waitTime;
            customer.TotalWaitTime += waitTime;

            ScheduleEvent(EventType.ServiceComplete, _currentTime + serviceTime, customer, _paymentStage1.Name);
        }

        private void ProcessOutputBufferV1(Fragment1 fragment, Action<Customer> nextAction)
        {
            if (fragment.OutputBuffer.Count > 0)
            {
                var customer = fragment.OutputBuffer.Dequeue();
                nextAction(customer);
            }

            if (fragment.Channel.Queue.Count > 0 && fragment.IsValveOpen())
            {
                if (fragment.Name == _firstDishStage1.Name)
                    StartFirstDishServiceV1();
                else if (fragment.Name == _secondDishStage1.Name)
                    StartSecondDishServiceV1();
            }
        }

        private void CompleteServiceV1(Event evt)
        {
            var customer = evt.Customer;
            string stageName = evt.StageName;

            if (stageName == _firstDishStage1.Name)
            {
                _firstDishStage1.Channel.IsBusy = false;
                _firstDishStage1.Channel.ServedCount++;
                _firstDishStage1.Channel.BusyTime += _currentTime - customer.FirstStartWait;
                _firstDishStage1.OutputBuffer.Enqueue(customer);
                ProcessOutputBufferV1(_firstDishStage1, ProcessSecondDishV1);
            }
            else if (stageName == _secondDishStage1.Name)
            {
                _secondDishStage1.Channel.IsBusy = false;
                _secondDishStage1.Channel.ServedCount++;
                _secondDishStage1.Channel.BusyTime += _currentTime - customer.SecondStartWait;
                _secondDishStage1.OutputBuffer.Enqueue(customer);
                ProcessOutputBufferV1(_secondDishStage1, ProcessDrinkV1);
            }
            else if (stageName == _drinkStage1.Name)
            {
                int channel = evt.ChannelIndex;
                _drinkStage1.IsBusy[channel] = false;
                _drinkStage1.ServedCount++;
                _drinkStage1.BusyTime[channel] += _currentTime - customer.DrinkStartWait;

                ProcessBreadV1(customer);

                if (_drinkStage1.Queue.Count > 0)
                {
                    var nextCustomer = _drinkStage1.Queue.Dequeue();
                    int freeChannel = _drinkStage1.GetFreeChannel();
                    if (freeChannel >= 0)
                        StartDrinkServiceV1(nextCustomer, freeChannel);
                }
            }
            else if (stageName == _breadStage1.Name)
            {
                int channel = evt.ChannelIndex;
                _breadStage1.IsBusy[channel] = false;
                _breadStage1.ServedCount++;
                _breadStage1.BusyTime[channel] += _currentTime - customer.BreadStartWait;

                ProcessCutleryV1(customer);

                if (_breadStage1.Queue.Count > 0)
                {
                    var nextCustomer = _breadStage1.Queue.Dequeue();
                    int freeChannel = _breadStage1.GetFreeChannel();
                    if (freeChannel >= 0)
                        StartBreadServiceV1(nextCustomer, freeChannel);
                }
            }
            else if (stageName == _cutleryStage1.Name)
            {
                _cutleryStage1.IsBusy = false;
                _cutleryStage1.ServedCount++;
                _cutleryStage1.BusyTime += _currentTime - customer.CutleryStartWait;

                ProcessPaymentV1(customer);

                if (_cutleryStage1.Queue.Count > 0)
                {
                    var nextCustomer = _cutleryStage1.Queue.Dequeue();
                    StartCutleryServiceV1(nextCustomer);
                }
            }
            else if (stageName == _paymentStage1.Name)
            {
                _paymentStage1.IsBusy = false;
                _paymentStage1.ServedCount++;
                _paymentStage1.BusyTime += _currentTime - customer.PaymentStartWait;

                if (customer.WantFirst) _firstDishTakenV1++;
                if (customer.WantSecond) _secondDishTakenV1++;
                if (customer.WantJuice) _juiceTakenV1++;
                else if (!customer.WantJuice) _teaTakenV1++;
                if (customer.WantBread) _breadTakenV1++;
                if (customer.Disturbance) _disturbanceCountV1++;

                if (!customer.WantFirst && !customer.WantSecond && !customer.WantBread)
                {
                    _noServiceNeededV1++;
                }

                double totalService = (customer.WantFirst ? customer.FirstServiceTime : 0) +
                                      (customer.WantSecond ? customer.SecondServiceTime : 0) +
                                      (customer.WantBread ? customer.BreadServiceTime : 0) +
                                      customer.DrinkServiceTime +
                                      customer.CutleryServiceTime +
                                      customer.PaymentServiceTime;
                _totalServiceTimeV1 += totalService;
                _totalMealTimeV1 += (_currentTime - customer.EntryTime);

                customer.HasTray = false;
                _activeCustomers--;
                _freeTrays++;

                customer.ExitTime = _currentTime;
                _servedCustomers++;
                _totalSystemTime += customer.ExitTime - customer.EntryTime;
                _totalWaitTime += customer.TotalWaitTime;

                if (_paymentStage1.Queue.Count > 0)
                {
                    var nextCustomer = _paymentStage1.Queue.Dequeue();
                    StartPaymentServiceV1(nextCustomer);
                }
            }
        }

        private void RunSingleDayV1()
        {
            ResetDay();
            InitVariant1();

            ScheduleEvent(EventType.GroupArrival, 0, null);

            while (_eventQueue.Count > 0 && !IsSimulationComplete())
            {
                var evt = _eventQueue.Dequeue();
                _currentTime = evt.Time;

                if (evt.Type == EventType.GroupArrival || evt.Type == EventType.CustomerArrival)
                    ProcessArrivalV1(evt);
                else if (evt.Type == EventType.ServiceComplete)
                    CompleteServiceV1(evt);
            }

            // Сохраняем результаты дня
            FirstDishTaken = _firstDishTakenV1;
            SecondDishTaken = _secondDishTakenV1;
            JuiceTaken = _juiceTakenV1;
            TeaTaken = _teaTakenV1;
            BreadTaken = _breadTakenV1;
            DisturbanceCount = _disturbanceCountV1;
            NoServiceNeeded = _noServiceNeededV1;
            TotalCustomers = _totalCustomers;

            if (_servedCustomers > 0)
            {
                AvgSystemTime = _totalSystemTime / _servedCustomers;
                AvgWaitTime = _totalWaitTime / _servedCustomers;
                AvgServiceTime = _totalServiceTimeV1 / _servedCustomers;
                AvgMealTime = _totalMealTimeV1 / _servedCustomers;
            }
        }

        #endregion

        #region Вариант 2

        private Customer CreateCustomerV2()
        {
            var customer = new Customer { Id = _nextCustomerId++ };
            customer.WantFirst = _rand.NextDouble() < 0.5;
            customer.WantSecond = _rand.NextDouble() < 0.8;
            customer.WantSalad = _rand.NextDouble() < 0.7;
            customer.HasAnyPaidItem = customer.WantFirst || customer.WantSecond || customer.WantSalad;
            return customer;
        }

        private void ProcessArrivalV2(Event evt)
        {
            if (IsSimulationComplete()) return;

            if (evt.Type == EventType.GroupArrival)
            {
                double nextInterval = Exponential(3.0);
                if (_currentTime + nextInterval < SIMULATION_TIME)
                    ScheduleEvent(EventType.GroupArrival, _currentTime + nextInterval, null);

                int groupSize = GetGroupSize();
                for (int i = 0; i < groupSize; i++)
                {
                    var customer = CreateCustomerV2();
                    ScheduleEvent(EventType.CustomerArrival, _currentTime, customer);
                }
            }
            else if (evt.Type == EventType.CustomerArrival)
            {
                if (!CanStartNewCustomer()) return;

                var customer = evt.Customer;
                _totalCustomers++;
                customer.EntryTime = _currentTime;
                customer.HasTray = true;
                _activeCustomers++;
                _freeTrays--;

                ProcessFirstDishV2(customer);
            }
        }

        private void ProcessFirstDishV2(Customer customer)
        {
            if (!customer.WantFirst)
            {
                ProcessSecondDishV2(customer);
                return;
            }

            customer.FirstStartWait = _currentTime;

            if (!_firstDishStage2.IsValveOpen())
            {
                customer.RejectedFirst = true;
                RejectFirstMeal++;
                _firstDishStage2.RejectCount++;
                ProcessSecondDishV2(customer);
                return;
            }

            _firstDishStage2.Buffer.Enqueue(customer);

            if (!_firstDishStage2.Channel.IsBusy)
                StartFirstDishServiceV2();
        }

        private void StartFirstDishServiceV2()
        {
            if (_firstDishStage2.Buffer.Count == 0) return;

            var customer = _firstDishStage2.Buffer.Dequeue();
            _firstDishStage2.Channel.IsBusy = true;

            double serviceTime = Uniform(_firstDishStage2.MinTime, _firstDishStage2.MaxTime);
            customer.FirstServiceTime = serviceTime;
            double waitTime = _currentTime - customer.FirstStartWait;
            _firstDishStage2.Channel.TotalWaitTime += waitTime;
            customer.TotalWaitTime += waitTime;

            ScheduleEvent(EventType.ServiceComplete, _currentTime + serviceTime, customer, _firstDishStage2.Name);
        }

        private void ProcessSecondDishV2(Customer customer)
        {
            customer.SecondStartWait = _currentTime;

            if (!customer.WantSecond)
            {
                ProcessSaladV2(customer);
                return;
            }

            int freeChannel = _secondDishStage2.GetFreeChannel();
            if (freeChannel >= 0)
                StartSecondDishServiceV2(customer, freeChannel);
            else
            {
                _secondDishStage2.Queue.Enqueue(customer);
                _secondDishStage2.UpdateQueueStats();
            }
        }

        private void StartSecondDishServiceV2(Customer customer, int channel)
        {
            _secondDishStage2.IsBusy[channel] = true;

            double serviceTime = Uniform(1.0, 2.0);
            customer.SecondServiceTime = serviceTime;
            double waitTime = _currentTime - customer.SecondStartWait;
            _secondDishStage2.TotalWaitTime += waitTime;
            customer.TotalWaitTime += waitTime;

            ScheduleEvent(EventType.ServiceComplete, _currentTime + serviceTime, customer, _secondDishStage2.Name, channel);
        }

        private void ProcessSaladV2(Customer customer)
        {
            if (!customer.WantSalad)
            {
                ProcessPaymentV2(customer);
                return;
            }

            int freeChannel = _saladStage2.GetFreeChannel();
            if (freeChannel >= 0)
                StartSaladServiceV2(customer, freeChannel);
            else
            {
                _saladStage2.Queue.Enqueue(customer);
                _saladStage2.UpdateQueueStats();
            }
        }

        private void StartSaladServiceV2(Customer customer, int channel)
        {
            _saladStage2.IsBusy[channel] = true;

            double serviceTime = Uniform(1.0, 2.0);
            customer.BreadServiceTime = serviceTime;
            double waitTime = _currentTime - customer.SecondStartWait;
            _saladStage2.TotalWaitTime += waitTime;
            customer.TotalWaitTime += waitTime;

            ScheduleEvent(EventType.ServiceComplete, _currentTime + serviceTime, customer, _saladStage2.Name, channel);
        }

        private void ProcessPaymentV2(Customer customer)
        {
            if (!customer.HasAnyPaidItem)
            {
                ProcessDrinkV2(customer);
                return;
            }

            customer.PaymentStartWait = _currentTime;

            if (!_paymentStage2.IsBusy)
                StartPaymentServiceV2(customer);
            else
            {
                _paymentStage2.Queue.Enqueue(customer);
                _paymentStage2.UpdateQueueStats();
            }
        }

        private void StartPaymentServiceV2(Customer customer)
        {
            _paymentStage2.IsBusy = true;

            double serviceTime = Normal(3.0, 1.5);
            customer.PaymentServiceTime = serviceTime;
            double waitTime = _currentTime - customer.PaymentStartWait;
            _paymentStage2.TotalWaitTime += waitTime;
            customer.TotalWaitTime += waitTime;

            ScheduleEvent(EventType.ServiceComplete, _currentTime + serviceTime, customer, _paymentStage2.Name);
        }

        private void ProcessDrinkV2(Customer customer)
        {
            if (!_drinkStage2.IsValveOpen())
            {
                customer.RejectedDrink = true;
                RejectDrink++;
                _drinkStage2.RejectCount++;
                CompleteCustomerV2(customer);
                return;
            }

            customer.DrinkStartWait = _currentTime;
            _drinkStage2.Buffer.Enqueue(customer);

            if (!_drinkStage2.Channel.IsBusy)
                StartDrinkServiceV2();
        }

        private void StartDrinkServiceV2()
        {
            if (_drinkStage2.Buffer.Count == 0) return;

            var customer = _drinkStage2.Buffer.Dequeue();
            _drinkStage2.Channel.IsBusy = true;

            double serviceTime = Uniform(_drinkStage2.MinTime, _drinkStage2.MaxTime);
            customer.DrinkServiceTime = serviceTime;
            double waitTime = _currentTime - customer.DrinkStartWait;
            _drinkStage2.Channel.TotalWaitTime += waitTime;
            customer.TotalWaitTime += waitTime;

            ScheduleEvent(EventType.ServiceComplete, _currentTime + serviceTime, customer, _drinkStage2.Name);
        }

        private void CompleteCustomerV2(Customer customer)
        {
            if (customer.WantFirst && !customer.RejectedFirst) _firstDishTakenV2++;
            if (customer.WantSecond) _secondDishTakenV2++;
            if (customer.WantSalad) _saladTakenV2++;
            if (!customer.RejectedDrink) _drinkTakenV2++;

            if (!customer.WantFirst && !customer.WantSecond && !customer.WantSalad && customer.RejectedDrink)
            {
                _noServiceNeededV2++;
            }

            double totalService = (customer.WantFirst && !customer.RejectedFirst ? customer.FirstServiceTime : 0) +
                                  (customer.WantSecond ? customer.SecondServiceTime : 0) +
                                  (customer.WantSalad ? customer.BreadServiceTime : 0) +
                                  (!customer.RejectedDrink ? customer.DrinkServiceTime : 0) +
                                  (customer.HasAnyPaidItem ? customer.PaymentServiceTime : 0);
            _totalServiceTimeV2 += totalService;
            _totalMealTimeV2 += (_currentTime - customer.EntryTime);

            customer.HasTray = false;
            _activeCustomers--;
            _freeTrays++;

            customer.ExitTime = _currentTime;
            _servedCustomers++;
            _totalSystemTime += customer.ExitTime - customer.EntryTime;
            _totalWaitTime += customer.TotalWaitTime;
        }

        private void CompleteServiceV2(Event evt)
        {
            var customer = evt.Customer;
            string stageName = evt.StageName;

            if (stageName == _firstDishStage2.Name)
            {
                _firstDishStage2.Channel.IsBusy = false;
                _firstDishStage2.Channel.ServedCount++;
                _firstDishStage2.Channel.BusyTime += _currentTime - customer.FirstStartWait;

                ProcessSecondDishV2(customer);
                StartFirstDishServiceV2();
            }
            else if (stageName == _secondDishStage2.Name)
            {
                int channel = evt.ChannelIndex;
                _secondDishStage2.IsBusy[channel] = false;
                _secondDishStage2.ServedCount++;
                _secondDishStage2.BusyTime[channel] += _currentTime - customer.SecondStartWait;

                ProcessSaladV2(customer);

                if (_secondDishStage2.Queue.Count > 0)
                {
                    var nextCustomer = _secondDishStage2.Queue.Dequeue();
                    int freeChannel = _secondDishStage2.GetFreeChannel();
                    if (freeChannel >= 0)
                        StartSecondDishServiceV2(nextCustomer, freeChannel);
                }
            }
            else if (stageName == _saladStage2.Name)
            {
                int channel = evt.ChannelIndex;
                _saladStage2.IsBusy[channel] = false;
                _saladStage2.ServedCount++;
                _saladStage2.BusyTime[channel] += _currentTime - customer.SecondStartWait;

                ProcessPaymentV2(customer);

                if (_saladStage2.Queue.Count > 0)
                {
                    var nextCustomer = _saladStage2.Queue.Dequeue();
                    int freeChannel = _saladStage2.GetFreeChannel();
                    if (freeChannel >= 0)
                        StartSaladServiceV2(nextCustomer, freeChannel);
                }
            }
            else if (stageName == _paymentStage2.Name)
            {
                _paymentStage2.IsBusy = false;
                _paymentStage2.ServedCount++;
                _paymentStage2.BusyTime += _currentTime - customer.PaymentStartWait;

                ProcessDrinkV2(customer);

                if (_paymentStage2.Queue.Count > 0)
                {
                    var nextCustomer = _paymentStage2.Queue.Dequeue();
                    StartPaymentServiceV2(nextCustomer);
                }
            }
            else if (stageName == _drinkStage2.Name)
            {
                _drinkStage2.Channel.IsBusy = false;
                _drinkStage2.Channel.ServedCount++;
                _drinkStage2.Channel.BusyTime += _currentTime - customer.DrinkStartWait;

                CompleteCustomerV2(customer);
                StartDrinkServiceV2();
            }
        }

        private void RunSingleDayV2()
        {
            ResetDay();
            InitVariant2();
            RejectFirstMeal = 0;
            RejectDrink = 0;

            ScheduleEvent(EventType.GroupArrival, 0, null);

            while (_eventQueue.Count > 0 && !IsSimulationComplete())
            {
                var evt = _eventQueue.Dequeue();
                _currentTime = evt.Time;

                if (evt.Type == EventType.GroupArrival || evt.Type == EventType.CustomerArrival)
                    ProcessArrivalV2(evt);
                else if (evt.Type == EventType.ServiceComplete)
                    CompleteServiceV2(evt);
            }

            // Сохраняем результаты дня
            FirstDishTaken = _firstDishTakenV2;
            SecondDishTaken = _secondDishTakenV2;
            SaladTaken = _saladTakenV2;
            DrinkTaken = _drinkTakenV2;
            NoServiceNeeded = _noServiceNeededV2;
            TotalCustomers = _totalCustomers;
            RejectFirstMealResult = RejectFirstMeal;
            RejectDrinkResult = RejectDrink;
            RejectFirstMealPercent = TotalCustomers > 0 ? (double)RejectFirstMeal / TotalCustomers * 100 : 0;
            RejectDrinkPercent = TotalCustomers > 0 ? (double)RejectDrink / TotalCustomers * 100 : 0;

            if (_servedCustomers > 0)
            {
                AvgSystemTime = _totalSystemTime / _servedCustomers;
                AvgWaitTime = _totalWaitTime / _servedCustomers;
                AvgServiceTime = _totalServiceTimeV2 / _servedCustomers;
                AvgMealTime = _totalMealTimeV2 / _servedCustomers;
            }
        }

        #endregion

        #region Общие методы

        private void ScheduleEvent(EventType type, double time, Customer customer, string stageName = null, int channelIndex = -1)
        {
            var evt = new Event
            {
                Time = time,
                Type = type,
                Customer = customer,
                StageName = stageName,
                ChannelIndex = channelIndex
            };
            _eventQueue.Enqueue(evt, time);
        }

        // Одна реализация = 3 дня по 9 часов для варианта 1 (по умолчанию)
        public void RunMultiDayV1()
        {
            RunMultiDayWithDaysV1(3);
        }

        // Одна реализация = 3 дня по 9 часов для варианта 2 (по умолчанию)
        public void RunMultiDayV2()
        {
            RunMultiDayWithDaysV2(3);
        }

        // Метод для запуска варианта 1 с произвольным количеством дней
        public void RunMultiDayWithDaysV1(int daysCount)
        {
            // Сброс общей статистики
            _firstDishTakenV1 = 0;
            _secondDishTakenV1 = 0;
            _juiceTakenV1 = 0;
            _teaTakenV1 = 0;
            _breadTakenV1 = 0;
            _disturbanceCountV1 = 0;
            _noServiceNeededV1 = 0;
            _totalServiceTimeV1 = 0;
            _totalMealTimeV1 = 0;
            _totalCustomers = 0;
            _servedCustomers = 0;
            _totalSystemTime = 0;
            _totalWaitTime = 0;

            for (int day = 1; day <= daysCount; day++)
            {
                RunSingleDayV1();

                _firstDishTakenV1 += FirstDishTaken;
                _secondDishTakenV1 += SecondDishTaken;
                _juiceTakenV1 += JuiceTaken;
                _teaTakenV1 += TeaTaken;
                _breadTakenV1 += BreadTaken;
                _disturbanceCountV1 += DisturbanceCount;
                _noServiceNeededV1 += NoServiceNeeded;
                _totalServiceTimeV1 += AvgServiceTime * TotalCustomers;
                _totalMealTimeV1 += AvgMealTime * TotalCustomers;
                _totalCustomers += TotalCustomers;
                _totalSystemTime += AvgSystemTime * TotalCustomers;
                _totalWaitTime += AvgWaitTime * TotalCustomers;
                _servedCustomers += TotalCustomers;
            }

            TotalCustomers = _totalCustomers;
            FirstDishTaken = _firstDishTakenV1;
            SecondDishTaken = _secondDishTakenV1;
            JuiceTaken = _juiceTakenV1;
            TeaTaken = _teaTakenV1;
            BreadTaken = _breadTakenV1;
            DisturbanceCount = _disturbanceCountV1;
            NoServiceNeeded = _noServiceNeededV1;

            if (_servedCustomers > 0)
            {
                AvgSystemTime = _totalSystemTime / _servedCustomers;
                AvgWaitTime = _totalWaitTime / _servedCustomers;
                AvgServiceTime = _totalServiceTimeV1 / _servedCustomers;
                AvgMealTime = _totalMealTimeV1 / _servedCustomers;
            }
        }

        // Метод для запуска варианта 2 с произвольным количеством дней
        public void RunMultiDayWithDaysV2(int daysCount)
        {
            _firstDishTakenV2 = 0;
            _secondDishTakenV2 = 0;
            _saladTakenV2 = 0;
            _drinkTakenV2 = 0;
            _noServiceNeededV2 = 0;
            _totalServiceTimeV2 = 0;
            _totalMealTimeV2 = 0;
            _totalCustomers = 0;
            _servedCustomers = 0;
            _totalSystemTime = 0;
            _totalWaitTime = 0;
            int totalRejectFirst = 0;
            int totalRejectDrink = 0;

            for (int day = 1; day <= daysCount; day++)
            {
                RunSingleDayV2();

                _firstDishTakenV2 += FirstDishTaken;
                _secondDishTakenV2 += SecondDishTaken;
                _saladTakenV2 += SaladTaken;
                _drinkTakenV2 += DrinkTaken;
                _noServiceNeededV2 += NoServiceNeeded;
                _totalServiceTimeV2 += AvgServiceTime * TotalCustomers;
                _totalMealTimeV2 += AvgMealTime * TotalCustomers;
                _totalCustomers += TotalCustomers;
                _totalSystemTime += AvgSystemTime * TotalCustomers;
                _totalWaitTime += AvgWaitTime * TotalCustomers;
                _servedCustomers += TotalCustomers;
                totalRejectFirst += RejectFirstMealResult;
                totalRejectDrink += RejectDrinkResult;
            }

            TotalCustomers = _totalCustomers;
            FirstDishTaken = _firstDishTakenV2;
            SecondDishTaken = _secondDishTakenV2;
            SaladTaken = _saladTakenV2;
            DrinkTaken = _drinkTakenV2;
            NoServiceNeeded = _noServiceNeededV2;
            RejectFirstMealResult = totalRejectFirst;
            RejectDrinkResult = totalRejectDrink;
            RejectFirstMealPercent = TotalCustomers > 0 ? (double)totalRejectFirst / TotalCustomers * 100 : 0;
            RejectDrinkPercent = TotalCustomers > 0 ? (double)totalRejectDrink / TotalCustomers * 100 : 0;

            if (_servedCustomers > 0)
            {
                AvgSystemTime = _totalSystemTime / _servedCustomers;
                AvgWaitTime = _totalWaitTime / _servedCustomers;
                AvgServiceTime = _totalServiceTimeV2 / _servedCustomers;
                AvgMealTime = _totalMealTimeV2 / _servedCustomers;
            }
        }

        // Универсальный метод
        public void RunMultiDayWithDays(int daysCount)
        {
            // Выбираем вариант на основе текущего состояния (костыль для совместимости)
            // В реальном коде лучше разделить
            if (_firstDishStage1 != null || true) // упрощённо
            {
                RunMultiDayWithDaysV1(daysCount);
                // Также сохраняем данные для варианта 2
                var tempSim = new CafeSimulation();
                tempSim.RunMultiDayWithDaysV2(daysCount);
                _saladTakenV2 = tempSim.SaladTaken;
                _drinkTakenV2 = tempSim.DrinkTaken;
                RejectFirstMealResult = tempSim.RejectFirstMealResult;
                RejectDrinkResult = tempSim.RejectDrinkResult;
                RejectFirstMealPercent = tempSim.RejectFirstMealPercent;
                RejectDrinkPercent = tempSim.RejectDrinkPercent;
                SaladTaken = tempSim.SaladTaken;
                DrinkTaken = tempSim.DrinkTaken;
            }
        }

        // Совместимость со старым интерфейсом
        public void RunVariant1() => RunMultiDayWithDaysV1(3);
        public void RunVariant2() => RunMultiDayWithDaysV2(3);

        private void Reset()
        {
            _currentTime = 0;
            _nextCustomerId = 1;
            _freeTrays = TOTAL_TRAYS;
            _activeCustomers = 0;
            _totalCustomers = 0;
            _servedCustomers = 0;
            _totalSystemTime = 0;
            _totalWaitTime = 0;
            _eventQueue.Clear();

            _firstDishTakenV1 = 0;
            _secondDishTakenV1 = 0;
            _juiceTakenV1 = 0;
            _teaTakenV1 = 0;
            _breadTakenV1 = 0;
            _disturbanceCountV1 = 0;
            _noServiceNeededV1 = 0;
            _totalServiceTimeV1 = 0;
            _totalMealTimeV1 = 0;

            _firstDishTakenV2 = 0;
            _secondDishTakenV2 = 0;
            _saladTakenV2 = 0;
            _drinkTakenV2 = 0;
            _noServiceNeededV2 = 0;
            _totalServiceTimeV2 = 0;
            _totalMealTimeV2 = 0;
        }

        #endregion
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                     МОДЕЛИРОВАНИЕ РАБОТЫ КАФЕ                      ║");
            Console.WriteLine("║          Время моделирования: 3 дня по 9 часов (27 часов)          ║");
            Console.WriteLine("║                        Ограничение: 30 подносов                    ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // 4 уровня относительной погрешности: 1%, 5%, 15%, 45%
            double[] relativeEpsilons = { 0.01, 0.05, 0.15, 0.45 };
            const double T_ALPHA = 1.895;
            const int INITIAL_RUNS = 10;
            const int MAX_ITERATIONS = 10;

            // Результаты для варианта 1
            Console.WriteLine(new string('=', 70));
            Console.WriteLine("ВАРИАНТ 1 (3 дня по 9 часов = 1 реализация)");
            Console.WriteLine(new string('=', 70));

            var resultsV1List = new List<AccuracyResults>();
            foreach (var epsilon in relativeEpsilons)
            {
                Console.WriteLine($"\n>>> ПОГРЕШНОСТЬ: {epsilon * 100}% от среднего времени ожидания <<<\n");
                var result = RunWithRelativeAccuracy(epsilon, T_ALPHA, INITIAL_RUNS, MAX_ITERATIONS, runVariant1: true);
                resultsV1List.Add(result);
                PrintShortResult(result, epsilon);
                Console.WriteLine(new string('-', 70));
            }

            // Результаты для варианта 2
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("ВАРИАНТ 2 (3 дня по 9 часов = 1 реализация)");
            Console.WriteLine(new string('=', 70));

            var resultsV2List = new List<AccuracyResults>();
            foreach (var epsilon in relativeEpsilons)
            {
                Console.WriteLine($"\n>>> ПОГРЕШНОСТЬ: {epsilon * 100}% от среднего времени ожидания <<<\n");
                var result = RunWithRelativeAccuracy(epsilon, T_ALPHA, INITIAL_RUNS, MAX_ITERATIONS, runVariant1: false);
                resultsV2List.Add(result);
                PrintShortResult(result, epsilon);
                Console.WriteLine(new string('-', 70));
            }

            // Сводные таблицы
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("СВОДНАЯ ТАБЛИЦА ДЛЯ ВАРИАНТА 1");
            Console.WriteLine(new string('=', 70));
            PrintSummaryTable(resultsV1List, relativeEpsilons);

            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("СВОДНАЯ ТАБЛИЦА ДЛЯ ВАРИАНТА 2");
            Console.WriteLine(new string('=', 70));
            PrintSummaryTable(resultsV2List, relativeEpsilons);

            // Детальные результаты последнего прогона для варианта 1
            if (resultsV1List.Last().LastSimulation != null)
                PrintDetailedResults(resultsV1List.Last().LastSimulation, 1);

            // Детальные результаты последнего прогона для варианта 2
            if (resultsV2List.Last().LastSimulation != null)
                PrintDetailedResults(resultsV2List.Last().LastSimulation, 2);

            // НОВОЕ: Исследование влияния количества дней в реализации
            RunDaysComparison();

            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("Моделирование завершено. Нажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        class AccuracyResults
        {
            public List<double> WaitTimes { get; set; } = new List<double>();
            public double MeanWaitTime { get; set; }
            public double Sigma { get; set; }
            public double RequiredN { get; set; }
            public int TotalRuns { get; set; }
            public bool AccuracyAchieved { get; set; }
            public CafeSimulation LastSimulation { get; set; }
            public double RelativeEpsilon { get; set; }
            public int IterationsCount { get; set; }
        }

        class DaysComparisonResult
        {
            public int DaysInRun { get; set; }
            public double MeanWaitTime { get; set; }
            public double Sigma { get; set; }
            public double RequiredN { get; set; }
            public int TotalRuns { get; set; }
            public bool AccuracyAchieved { get; set; }
            public int IterationsCount { get; set; }
            public double TotalSimulatedHours { get; set; }
            public double TotalSimulatedMinutes { get; set; }
            public CafeSimulation LastSimulation { get; set; }
        }

        static AccuracyResults RunWithRelativeAccuracy(double relativeEpsilon, double tAlpha, int initialRuns, int maxIterations, bool runVariant1)
        {
            var results = new AccuracyResults();
            results.RelativeEpsilon = relativeEpsilon;
            int currentRuns = initialRuns;
            int iteration = 0;
            bool accuracyAchieved = false;

            while (!accuracyAchieved && iteration < maxIterations)
            {
                iteration++;
                Console.WriteLine($"--- ИТЕРАЦИЯ {iteration}: выполняется {currentRuns} реализаций (каждая = 3 дня) ---");

                for (int run = 1; run <= currentRuns; run++)
                {
                    var simulation = new CafeSimulation();

                    if (runVariant1)
                        simulation.RunVariant1();
                    else
                        simulation.RunVariant2();

                    results.WaitTimes.Add(simulation.AvgWaitTime);
                    results.LastSimulation = simulation;

                    Console.WriteLine($"  Реализация {run,2}: Ср. время ожидания = {simulation.AvgWaitTime,6:F3} мин.");
                }

                if (results.WaitTimes.Count >= 2)
                {
                    double mean = results.WaitTimes.Average();
                    double variance = results.WaitTimes.Select(v => Math.Pow(v - mean, 2)).Sum() / (results.WaitTimes.Count - 1);
                    double sigma = Math.Sqrt(variance);

                    double absoluteEpsilon = relativeEpsilon * mean;
                    double requiredN = Math.Ceiling((sigma * sigma / (absoluteEpsilon * absoluteEpsilon)) * tAlpha * tAlpha);

                    results.MeanWaitTime = mean;
                    results.Sigma = sigma;
                    results.RequiredN = requiredN;
                    results.TotalRuns = results.WaitTimes.Count;
                    results.IterationsCount = iteration;

                    Console.WriteLine($"\n  Текущее среднее время ожидания: {mean:F3} мин.");
                    Console.WriteLine($"  Заданная относительная погрешность: {relativeEpsilon * 100}%");
                    Console.WriteLine($"  Соответствующая абсолютная погрешность: {absoluteEpsilon:F3} мин.");
                    Console.WriteLine($"  Среднеквадратическое отклонение (σ): {sigma:F3} мин.");
                    Console.WriteLine($"  Необходимое число реализаций (N*): {requiredN:F0}");

                    if (requiredN <= results.WaitTimes.Count)
                    {
                        Console.WriteLine($"  ✓ Требуемая точность достигнута!");
                        accuracyAchieved = true;
                        results.AccuracyAchieved = true;
                    }
                    else
                    {
                        int additionalNeeded = (int)Math.Ceiling(requiredN - results.WaitTimes.Count);
                        Console.WriteLine($"  ✗ Требуемая точность НЕ достигнута. Необходимо добавить {additionalNeeded} реализаций.");
                        currentRuns = additionalNeeded;
                    }
                }
                else
                {
                    Console.WriteLine($"  Недостаточно данных для анализа точности (нужно минимум 2 реализации)");
                    currentRuns = 5;
                }
                Console.WriteLine();
            }

            if (!accuracyAchieved)
            {
                Console.WriteLine($"\n⚠ Внимание: максимальное количество итераций ({maxIterations}) достигнуто.");
                if (results.WaitTimes.Count > 0)
                {
                    results.MeanWaitTime = results.WaitTimes.Average();
                }
            }

            return results;
        }

        static void PrintShortResult(AccuracyResults results, double epsilon)
        {
            const double T_ALPHA = 1.895;
            double intervalHalfWidth = T_ALPHA * results.Sigma / Math.Sqrt(results.TotalRuns);
            double actualRelativeError = (intervalHalfWidth / results.MeanWaitTime) * 100;

            Console.WriteLine($"\n=== РЕЗУЛЬТАТ ДЛЯ ПОГРЕШНОСТИ {epsilon * 100}% ===");
            Console.WriteLine($"  Среднее время ожидания: {results.MeanWaitTime:F3} мин.");
            Console.WriteLine($"  Среднеквадратическое отклонение (σ): {results.Sigma:F3} мин.");
            Console.WriteLine($"  Фактическое число реализаций: {results.TotalRuns}");
            Console.WriteLine($"  Необходимое число реализаций (N*): {results.RequiredN:F0}");
            Console.WriteLine($"  Количество итераций: {results.IterationsCount}");
            Console.WriteLine($"  Доверительный интервал (90%): [{results.MeanWaitTime - intervalHalfWidth:F3}; {results.MeanWaitTime + intervalHalfWidth:F3}] мин.");
            Console.WriteLine($"  Фактическая относительная погрешность: {actualRelativeError:F2}%");
            Console.WriteLine($"  Точность: {(results.AccuracyAchieved ? "ДОСТИГНУТА ✓" : "НЕ ДОСТИГНУТА ✗")}");
        }

        static void PrintSummaryTable(List<AccuracyResults> resultsList, double[] epsilons)
        {
            Console.WriteLine("\n┌────────────┬────────────────┬────────────────┬────────────────┬────────────────────┬────────────────────┐");
            Console.WriteLine("│ Погрешность│  Среднее время │   СКО (σ)      │  Реализаций    │  Необходимо N*     │  Фактическая       │");
            Console.WriteLine("│     (%)    │  ожидания (мин)│   (мин)        │  (факт)        │                    │  погрешность (%)   │");
            Console.WriteLine("├────────────┼────────────────┼────────────────┼────────────────┼────────────────────┼────────────────────┤");

            const double T_ALPHA = 1.895;
            for (int i = 0; i < resultsList.Count; i++)
            {
                var res = resultsList[i];
                double intervalHalfWidth = T_ALPHA * res.Sigma / Math.Sqrt(res.TotalRuns);
                double actualRelativeError = (intervalHalfWidth / res.MeanWaitTime) * 100;

                Console.WriteLine($"│ {epsilons[i] * 100,8:F0}%   │ {res.MeanWaitTime,14:F3} │ {res.Sigma,14:F3} │ {res.TotalRuns,14} │ {res.RequiredN,18:F0} │ {actualRelativeError,18:F2} │");
            }

            Console.WriteLine("└────────────┴────────────────┴────────────────┴────────────────┴────────────────────┴────────────────────┘");

            Console.WriteLine("\nПримечание:");
            Console.WriteLine("  - Одна реализация = 3 рабочих дня по 9 часов");
            Console.WriteLine("  - Необходимо N* = (σ² / (ε·X̄)²) · t², где ε - относительная погрешность");
            Console.WriteLine("  - Фактическая погрешность = (t·σ/√n) / X̄ · 100%");
        }

        static void PrintDetailedResults(CafeSimulation sim, int variant)
        {
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine($"ДЕТАЛЬНЫЕ РЕЗУЛЬТАТЫ ПОСЛЕДНЕГО ПРОГОНА (ВАРИАНТ {variant})");
            Console.WriteLine(new string('=', 70));

            Console.WriteLine($"Всего обслужено за 3 дня: {sim.TotalCustomers} человек");
            Console.WriteLine($"Среднее время пребывания в системе: {sim.AvgSystemTime:F2} мин.");
            Console.WriteLine($"Среднее время ожидания в очередях: {sim.AvgWaitTime:F2} мин.");
            Console.WriteLine($"Среднее время обслуживания: {sim.AvgServiceTime:F2} мин.");
            Console.WriteLine($"Среднее время получения обеда: {sim.AvgMealTime:F2} мин.");

            if (variant == 1)
            {
                Console.WriteLine($"\nСТАТИСТИКА ПО БЛЮДАМ (ВАРИАНТ 1):");
                Console.WriteLine($"  Первое блюдо: {sim.FirstDishTaken} ({(sim.FirstDishTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"  Второе блюдо: {sim.SecondDishTaken} ({(sim.SecondDishTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"  Сок: {sim.JuiceTaken} ({(sim.JuiceTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"  Чай: {sim.TeaTaken} ({(sim.TeaTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"  Хлебобулочные: {sim.BreadTaken} ({(sim.BreadTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"  Возмущающее воздействие: {sim.DisturbanceCount} ({(sim.DisturbanceCount * 100.0 / sim.TotalCustomers):F1}%)");
            }
            else
            {
                Console.WriteLine($"\nСТАТИСТИКА ПО БЛЮДАМ (ВАРИАНТ 2):");
                Console.WriteLine($"  Первое блюдо: {sim.FirstDishTaken} ({(sim.FirstDishTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"  Второе блюдо: {sim.SecondDishTaken} ({(sim.SecondDishTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"  Салат: {sim.SaladTaken} ({(sim.SaladTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"  Напиток: {sim.DrinkTaken} ({(sim.DrinkTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"\nОТКАЗЫ:");
                Console.WriteLine($"  Отказов от первого блюда: {sim.RejectFirstMealResult} ({sim.RejectFirstMealPercent:F1}%)");
                Console.WriteLine($"  Отказов от напитка: {sim.RejectDrinkResult} ({sim.RejectDrinkPercent:F1}%)");
            }
            Console.WriteLine($"\nЧисло людей, которым не понадобилось обслуживание: {sim.NoServiceNeeded}");
        }

        // НОВЫЙ МЕТОД: Исследование влияния количества дней в реализации
        static void RunDaysComparison()
        {
            const double FIXED_EPSILON = 0.05;  // Фиксированная погрешность 5%
            const double T_ALPHA = 1.895;
            const int INITIAL_RUNS = 10;
            const int MAX_ITERATIONS = 10;
            int[] daysOptions = { 1, 3, 10 };

            Console.WriteLine("\n" + new string('=', 90));
            Console.WriteLine($"ИССЛЕДОВАНИЕ ВЛИЯНИЯ КОЛИЧЕСТВА ДНЕЙ В РЕАЛИЗАЦИИ (ε = {FIXED_EPSILON * 100}%)");
            Console.WriteLine(new string('=', 90));

            // Для варианта 1
            Console.WriteLine("\n" + new string('=', 90));
            Console.WriteLine("ВАРИАНТ 1");
            Console.WriteLine(new string('=', 90));

            var resultsV1Days = new List<DaysComparisonResult>();

            foreach (int days in daysOptions)
            {
                Console.WriteLine($"\n>>> {days} ДНЕЙ В ОДНОЙ РЕАЛИЗАЦИИ (всего {days * 9} часов) <<<\n");

                var result = RunWithRelativeAccuracyForDays(FIXED_EPSILON, T_ALPHA, INITIAL_RUNS, MAX_ITERATIONS, runVariant1: true, daysInRun: days);
                resultsV1Days.Add(result);

                PrintDaysResult(result, days);
                Console.WriteLine(new string('-', 90));
            }

            // Для варианта 2
            Console.WriteLine("\n" + new string('=', 90));
            Console.WriteLine("ВАРИАНТ 2");
            Console.WriteLine(new string('=', 90));

            var resultsV2Days = new List<DaysComparisonResult>();

            foreach (int days in daysOptions)
            {
                Console.WriteLine($"\n>>> {days} ДНЕЙ В ОДНОЙ РЕАЛИЗАЦИИ (всего {days * 9} часов) <<<\n");

                var result = RunWithRelativeAccuracyForDays(FIXED_EPSILON, T_ALPHA, INITIAL_RUNS, MAX_ITERATIONS, runVariant1: false, daysInRun: days);
                resultsV2Days.Add(result);

                PrintDaysResult(result, days);
                Console.WriteLine(new string('-', 90));
            }

            // Сводная таблица для варианта 1
            Console.WriteLine("\n" + new string('=', 90));
            Console.WriteLine("СВОДНАЯ ТАБЛИЦА ДЛЯ ВАРИАНТА 1 (ε = 5%)");
            Console.WriteLine(new string('=', 90));
            PrintDaysSummaryTable(resultsV1Days, daysOptions);

            // Сводная таблица для варианта 2
            Console.WriteLine("\n" + new string('=', 90));
            Console.WriteLine("СВОДНАЯ ТАБЛИЦА ДЛЯ ВАРИАНТА 2 (ε = 5%)");
            Console.WriteLine(new string('=', 90));
            PrintDaysSummaryTable(resultsV2Days, daysOptions);

            // Анализ эффективности
            Console.WriteLine("\n" + new string('=', 90));
            Console.WriteLine("АНАЛИЗ ЭФФЕКТИВНОСТИ");
            Console.WriteLine(new string('=', 90));
            AnalyzeEfficiency(resultsV1Days, resultsV2Days, daysOptions);
        }

        static DaysComparisonResult RunWithRelativeAccuracyForDays(double relativeEpsilon, double tAlpha, int initialRuns, int maxIterations, bool runVariant1, int daysInRun)
        {
            var result = new DaysComparisonResult();
            result.DaysInRun = daysInRun;
            result.TotalSimulatedHours = daysInRun * 9;
            result.TotalSimulatedMinutes = daysInRun * 540;

            List<double> waitTimes = new List<double>();
            int currentRuns = initialRuns;
            int iteration = 0;
            bool accuracyAchieved = false;

            while (!accuracyAchieved && iteration < maxIterations)
            {
                iteration++;
                Console.WriteLine($"--- ИТЕРАЦИЯ {iteration}: выполняется {currentRuns} реализаций (каждая = {daysInRun} дня) ---");

                for (int run = 1; run <= currentRuns; run++)
                {
                    var simulation = new CafeSimulation();

                    if (runVariant1)
                        simulation.RunMultiDayWithDaysV1(daysInRun);
                    else
                        simulation.RunMultiDayWithDaysV2(daysInRun);

                    waitTimes.Add(simulation.AvgWaitTime);
                    result.LastSimulation = simulation;

                    Console.WriteLine($"  Реализация {run,2}: Ср. время ожидания = {simulation.AvgWaitTime,6:F3} мин.");
                }

                if (waitTimes.Count >= 2)
                {
                    double mean = waitTimes.Average();
                    double variance = waitTimes.Select(v => Math.Pow(v - mean, 2)).Sum() / (waitTimes.Count - 1);
                    double sigma = Math.Sqrt(variance);

                    double absoluteEpsilon = relativeEpsilon * mean;
                    double requiredN = Math.Ceiling((sigma * sigma / (absoluteEpsilon * absoluteEpsilon)) * tAlpha * tAlpha);

                    result.MeanWaitTime = mean;
                    result.Sigma = sigma;
                    result.RequiredN = requiredN;
                    result.TotalRuns = waitTimes.Count;
                    result.IterationsCount = iteration;

                    Console.WriteLine($"\n  Текущее среднее время ожидания: {mean:F3} мин.");
                    Console.WriteLine($"  Заданная относительная погрешность: {relativeEpsilon * 100}%");
                    Console.WriteLine($"  Соответствующая абсолютная погрешность: {absoluteEpsilon:F3} мин.");
                    Console.WriteLine($"  Среднеквадратическое отклонение (σ): {sigma:F3} мин.");
                    Console.WriteLine($"  Необходимое число реализаций (N*): {requiredN:F0}");

                    if (requiredN <= waitTimes.Count)
                    {
                        Console.WriteLine($"  ✓ Требуемая точность достигнута!");
                        accuracyAchieved = true;
                        result.AccuracyAchieved = true;
                    }
                    else
                    {
                        int additionalNeeded = (int)Math.Ceiling(requiredN - waitTimes.Count);
                        Console.WriteLine($"  ✗ Требуемая точность НЕ достигнута. Необходимо добавить {additionalNeeded} реализаций.");
                        currentRuns = additionalNeeded;
                    }
                }
                else
                {
                    Console.WriteLine($"  Недостаточно данных для анализа точности (нужно минимум 2 реализации)");
                    currentRuns = 5;
                }
                Console.WriteLine();
            }

            if (!accuracyAchieved)
            {
                Console.WriteLine($"\n⚠ Внимание: максимальное количество итераций ({maxIterations}) достигнуто.");
                if (waitTimes.Count > 0)
                {
                    result.MeanWaitTime = waitTimes.Average();
                }
            }

            return result;
        }

        static void PrintDaysResult(DaysComparisonResult result, int days)
        {
            const double T_ALPHA = 1.895;
            double intervalHalfWidth = T_ALPHA * result.Sigma / Math.Sqrt(result.TotalRuns);
            double actualRelativeError = (intervalHalfWidth / result.MeanWaitTime) * 100;

            Console.WriteLine($"\n=== РЕЗУЛЬТАТ ДЛЯ {days} ДНЕЙ В РЕАЛИЗАЦИИ ===");
            Console.WriteLine($"  Всего моделируемых часов: {result.TotalSimulatedHours} ч ({result.TotalSimulatedMinutes} мин)");
            Console.WriteLine($"  Среднее время ожидания: {result.MeanWaitTime:F3} мин.");
            Console.WriteLine($"  Среднеквадратическое отклонение (σ): {result.Sigma:F3} мин.");
            Console.WriteLine($"  Фактическое число реализаций: {result.TotalRuns}");
            Console.WriteLine($"  Необходимое число реализаций (N*): {result.RequiredN:F0}");
            Console.WriteLine($"  Количество итераций: {result.IterationsCount}");
            Console.WriteLine($"  Доверительный интервал (90%): [{result.MeanWaitTime - intervalHalfWidth:F3}; {result.MeanWaitTime + intervalHalfWidth:F3}] мин.");
            Console.WriteLine($"  Фактическая относительная погрешность: {actualRelativeError:F2}%");
            Console.WriteLine($"  Точность: {(result.AccuracyAchieved ? "ДОСТИГНУТА ✓" : "НЕ ДОСТИГНУТА ✗")}");
        }

        static void PrintDaysSummaryTable(List<DaysComparisonResult> results, int[] daysOptions)
        {
            Console.WriteLine("\n┌──────────────┬────────────────┬────────────────┬────────────────┬────────────────────┬────────────────────┬────────────────────┐");
            Console.WriteLine("│ Дней в одной │  Среднее время │   СКО (σ)      │  Реализаций    │  Необходимо N*     │  Фактическая       │  Всего часов       │");
            Console.WriteLine("│ реализации   │  ожидания (мин)│   (мин)        │  (факт)        │                    │  погрешность (%)   │  моделирования     │");
            Console.WriteLine("├──────────────┼────────────────┼────────────────┼────────────────┼────────────────────┼────────────────────┼────────────────────┤");

            const double T_ALPHA = 1.895;
            for (int i = 0; i < results.Count; i++)
            {
                var res = results[i];
                double intervalHalfWidth = T_ALPHA * res.Sigma / Math.Sqrt(res.TotalRuns);
                double actualRelativeError = (intervalHalfWidth / res.MeanWaitTime) * 100;
                int totalHours = daysOptions[i] * 9;

                Console.WriteLine($"│ {daysOptions[i],12} │ {res.MeanWaitTime,14:F3} │ {res.Sigma,14:F3} │ {res.TotalRuns,14} │ {res.RequiredN,18:F0} │ {actualRelativeError,18:F2} │ {totalHours,18} │");
            }

            Console.WriteLine("└──────────────┴────────────────┴────────────────┴────────────────┴────────────────────┴────────────────────┴────────────────────┘");
        }

        static void AnalyzeEfficiency(List<DaysComparisonResult> resultsV1, List<DaysComparisonResult> resultsV2, int[] daysOptions)
        {
            Console.WriteLine("\n┌─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│                                         СРАВНИТЕЛЬНЫЙ АНАЛИЗ ЭФФЕКТИВНОСТИ                                                      │");
            Console.WriteLine("├──────────────┬──────────────────────────────┬──────────────────────────────┬──────────────────────────────────────────────────┤");
            Console.WriteLine("│ Дней в одной │        ВАРИАНТ 1             │        ВАРИАНТ 2             │   Общее время моделирования (часов)              │");
            Console.WriteLine("│ реализации   ├──────────────┬───────────────┼──────────────┬───────────────┼──────────────────────────────────────────────────┤");
            Console.WriteLine("│              │   N* (необх)  │   Сигма (σ)   │   N* (необх)  │   Сигма (σ)   │   V1               │   V2               │");
            Console.WriteLine("├──────────────┼──────────────┼───────────────┼──────────────┼───────────────┼────────────────────┼────────────────────┤");

            for (int i = 0; i < daysOptions.Length; i++)
            {
                int totalHours = daysOptions[i] * 9;
                double efficiencyV1 = totalHours * resultsV1[i].RequiredN;
                double efficiencyV2 = totalHours * resultsV2[i].RequiredN;

                Console.WriteLine($"│ {daysOptions[i],12} │ {resultsV1[i].RequiredN,12:F0} │ {resultsV1[i].Sigma,11:F3} │ {resultsV2[i].RequiredN,12:F0} │ {resultsV2[i].Sigma,11:F3} │ {efficiencyV1,18:F0} │ {efficiencyV2,18:F0} │");
            }

            Console.WriteLine("└──────────────┴──────────────┴───────────────┴──────────────┴───────────────┴────────────────────┴────────────────────┘");

            

            // Находим оптимальный вариант
            int bestDaysV1 = daysOptions[0];
            int bestDaysV2 = daysOptions[0];
            double bestEfficiencyV1 = daysOptions[0] * 9 * resultsV1[0].RequiredN;
            double bestEfficiencyV2 = daysOptions[0] * 9 * resultsV2[0].RequiredN;

            for (int i = 1; i < daysOptions.Length; i++)
            {
                double effV1 = daysOptions[i] * 9 * resultsV1[i].RequiredN;
                double effV2 = daysOptions[i] * 9 * resultsV2[i].RequiredN;
                if (effV1 < bestEfficiencyV1) { bestEfficiencyV1 = effV1; bestDaysV1 = daysOptions[i]; }
                if (effV2 < bestEfficiencyV2) { bestEfficiencyV2 = effV2; bestDaysV2 = daysOptions[i]; }
            }

            Console.WriteLine($"\n  Оптимальное количество дней в реализации для ВАРИАНТА 1: {bestDaysV1} дней");
            Console.WriteLine($"  Оптимальное количество дней в реализации для ВАРИАНТА 2: {bestDaysV2} дней");
        }
    }
}