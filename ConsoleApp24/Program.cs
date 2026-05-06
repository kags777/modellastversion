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

        // Для варианта 1
        public bool WantFirst { get; set; }
        public bool WantSecond { get; set; }
        public bool WantJuice { get; set; }
        public bool WantBread { get; set; }
        public bool Disturbance { get; set; }

        // Для варианта 2
        public bool WantSalad { get; set; }
        public bool RejectedFirst { get; set; }
        public bool RejectedDrink { get; set; }
        public bool HasAnyPaidItem { get; set; }

        // Времена начала этапов
        public double FirstStartWait { get; set; }
        public double SecondStartWait { get; set; }
        public double DrinkStartWait { get; set; }
        public double BreadStartWait { get; set; }
        public double CutleryStartWait { get; set; }
        public double PaymentStartWait { get; set; }

        // Для статистики времени обслуживания
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

        // Вариант 1
        private Fragment1 _firstDishStage1;
        private Fragment1 _secondDishStage1;
        private MultiChannelStage _drinkStage1;
        private MultiChannelStage _breadStage1;
        private ServiceStage _cutleryStage1;
        private ServiceStage _paymentStage1;

        // Вариант 2
        private Fragment4 _firstDishStage2;
        private MultiChannelStage _secondDishStage2;
        private MultiChannelStage _saladStage2;
        private ServiceStage _paymentStage2;
        private Fragment4 _drinkStage2;

        public int RejectFirstMeal { get; private set; }
        public int RejectDrink { get; private set; }

        // Дополнительная статистика для варианта 1
        private int _firstDishTakenV1;
        private int _secondDishTakenV1;
        private int _juiceTakenV1;
        private int _teaTakenV1;
        private int _breadTakenV1;
        private int _disturbanceCountV1;
        private int _noServiceNeededV1;
        private double _totalServiceTimeV1;
        private double _totalMealTimeV1;

        // Дополнительная статистика для варианта 2
        private int _firstDishTakenV2;
        private int _secondDishTakenV2;
        private int _saladTakenV2;
        private int _drinkTakenV2;
        private int _noServiceNeededV2;
        private double _totalServiceTimeV2;
        private double _totalMealTimeV2;

        // Результаты одного прогона
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
            // Проверяем наличие свободных подносов
            if (_freeTrays <= 0)
                return false;
            return true;
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
                if (!CanStartNewCustomer())
                {
                    // Если нет свободных подносов, посетитель уходит (не обслуживается)
                    return;
                }

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

                // Сбор статистики варианта 1
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
            // Сбор статистики варианта 2
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

        public void RunVariant1()
        {
            Reset();
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

            // Сохраняем результаты
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

        public void RunVariant2()
        {
            Reset();
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

            // Сохраняем результаты
            TotalCustomers = _totalCustomers;
            FirstDishTaken = _firstDishTakenV2;
            SecondDishTaken = _secondDishTakenV2;
            SaladTaken = _saladTakenV2;
            DrinkTaken = _drinkTakenV2;
            NoServiceNeeded = _noServiceNeededV2;
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

            // Сброс статистики варианта 1
            _firstDishTakenV1 = 0;
            _secondDishTakenV1 = 0;
            _juiceTakenV1 = 0;
            _teaTakenV1 = 0;
            _breadTakenV1 = 0;
            _disturbanceCountV1 = 0;
            _noServiceNeededV1 = 0;
            _totalServiceTimeV1 = 0;
            _totalMealTimeV1 = 0;

            // Сброс статистики варианта 2
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
            Console.WriteLine(" МОДЕЛИРОВАНИЕ РАБОТЫ КАФЕ");
            Console.WriteLine("Время моделирования: 9 часов (540 минут)");
            Console.WriteLine("Ограничение: 30 подносов");
            Console.WriteLine("Запуск моделирования...\n");

            const double EPSILON = 1.0;        // Заданная точность (в мин.)
            const double T_ALPHA = 1.895;      // Коэффициент Стьюдента для 90% доверия
            const int INITIAL_RUNS = 30;        // Начальное количество прогонов
            const int MAX_ITERATIONS = 10;     // Максимальное количество итераций

            // Моделирование для варианта 1 с обеспечением точности
            Console.WriteLine(new string('=', 60));
            Console.WriteLine("ВАРИАНТ 1");
            Console.WriteLine(new string('=', 60));
            var resultsV1 = RunWithAccuracy(EPSILON, T_ALPHA, INITIAL_RUNS, MAX_ITERATIONS, runVariant1: true);
            PrintVariant1Results(resultsV1);

            // Моделирование для варианта 2 с обеспечением точности
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("ВАРИАНТ 2");
            Console.WriteLine(new string('=', 60));
            var resultsV2 = RunWithAccuracy(EPSILON, T_ALPHA, INITIAL_RUNS, MAX_ITERATIONS, runVariant1: false);
            PrintVariant2Results(resultsV2);

            Console.WriteLine("\n" + new string('=', 60));
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
        }

        static AccuracyResults RunWithAccuracy(double epsilon, double tAlpha, int initialRuns, int maxIterations, bool runVariant1)
        {
            var results = new AccuracyResults();
            int currentRuns = initialRuns;
            int iteration = 0;
            bool accuracyAchieved = false;

            while (!accuracyAchieved && iteration < maxIterations)
            {
                iteration++;
                Console.WriteLine($"\n--- ИТЕРАЦИЯ {iteration}: выполняется {currentRuns} прогонов ---");

                // Выполняем прогоны
                for (int run = 1; run <= currentRuns; run++)
                {
                    var simulation = new CafeSimulation();

                    if (runVariant1)
                        simulation.RunVariant1();
                    else
                        simulation.RunVariant2();

                    results.WaitTimes.Add(simulation.AvgWaitTime);
                    results.LastSimulation = simulation;
                }

                // Анализ точности
                if (results.WaitTimes.Count >= 2)
                {
                    double mean = results.WaitTimes.Average();
                    double variance = results.WaitTimes.Select(v => Math.Pow(v - mean, 2)).Sum() / (results.WaitTimes.Count - 1);
                    double sigma = Math.Sqrt(variance);
                    double requiredN = (sigma * sigma / (epsilon * epsilon)) * tAlpha * tAlpha;

                    results.MeanWaitTime = mean;
                    results.Sigma = sigma;
                    results.RequiredN = requiredN;
                    results.TotalRuns = results.WaitTimes.Count;

                    Console.WriteLine($"  Среднее время ожидания: {mean:F3} мин.");
                    Console.WriteLine($"  Среднеквадратическое отклонение (σ): {sigma:F3} мин.");
                    Console.WriteLine($"  Необходимое число реализаций (N*): {requiredN:F2}");

                    if (requiredN <= results.WaitTimes.Count)
                    {
                        Console.WriteLine($"   Требуемая точность достигнута!");
                        accuracyAchieved = true;
                        results.AccuracyAchieved = true;
                    }
                    else
                    {
                        int additionalNeeded = (int)Math.Ceiling(requiredN - results.WaitTimes.Count);
                        Console.WriteLine($"   Требуемая точность НЕ достигнута. Необходимо добавить {additionalNeeded} прогонов.");
                        currentRuns = additionalNeeded;
                    }
                }
                else
                {
                    Console.WriteLine($"  Недостаточно данных для анализа точности (нужно минимум 2 прогона)");
                    currentRuns = 5;
                }
            }

            if (!accuracyAchieved)
            {
                Console.WriteLine($"\n Внимание: максимальное количество итераций ({maxIterations}) достигнуто.");
                if (results.WaitTimes.Count > 0)
                {
                    results.MeanWaitTime = results.WaitTimes.Average();
                }
            }

            return results;
        }

        static void PrintVariant1Results(AccuracyResults results)
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("РЕЗУЛЬТАТЫ МОДЕЛИРОВАНИЯ: ВАРИАНТ 1");
            Console.WriteLine(new string('=', 60));

            var sim = results.LastSimulation;
            if (sim != null)
            {
                Console.WriteLine($"Моделирование завершено по времени: 9 часов (540 мин.)");
                Console.WriteLine($"Всего вошло в систему: {sim.TotalCustomers}");
                Console.WriteLine($"Обслужено полностью: {sim.TotalCustomers}");

                // Вычисляем процент потерянных посетителей из-за отсутствия подносов
                int lostCustomers = Math.Abs(sim.TotalCustomers - (sim.FirstDishTaken + sim.SecondDishTaken + sim.BreadTaken));
                Console.WriteLine($"Потеряно из-за отсутствия подносов: {lostCustomers}");

                Console.WriteLine($"Среднее время пребывания в системе: {sim.AvgSystemTime:F2} мин.");
                Console.WriteLine($"Среднее время ожидания в очередях: {results.MeanWaitTime:F2} мин.");

                Console.WriteLine($"\nСТАТИСТИКА ПО БЛЮДАМ (ВАРИАНТ 1)");
                Console.WriteLine($"Получили первое блюдо: {sim.FirstDishTaken} ({(sim.FirstDishTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"Получили второе блюдо: {sim.SecondDishTaken} ({(sim.SecondDishTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"Взяли сок: {sim.JuiceTaken} ({(sim.JuiceTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"Взяли чай: {sim.TeaTaken} ({(sim.TeaTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"Взяли хлебобулочные: {sim.BreadTaken} ({(sim.BreadTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"Число людей, испытавших возмущающее воздействие: {sim.DisturbanceCount} ({(sim.DisturbanceCount * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"Число людей, которым не понадобилось обслуживание: {sim.NoServiceNeeded}");

                Console.WriteLine($"Среднее время обслуживания: {sim.AvgServiceTime:F2} мин.");
                Console.WriteLine($"Среднее время получения обеда: {sim.AvgMealTime:F2} мин.");
                Console.WriteLine($"Загруженность кассы: {(sim.AvgServiceTime > 0 ? (sim.AvgServiceTime / sim.AvgSystemTime * 100) : 0):F1}% от времени в системе");
            }

            // Вывод информации о точности
            Console.WriteLine($"\n--- ОБЕСПЕЧЕНИЕ ТОЧНОСТИ ---");
            Console.WriteLine($"Всего выполнено прогонов: {results.TotalRuns}");
            Console.WriteLine($"Среднее время ожидания: {results.MeanWaitTime:F2} мин.");
            Console.WriteLine($"Среднеквадратическое отклонение (σ): {results.Sigma:F3} мин.");
            Console.WriteLine($"Необходимое число реализаций (N*): {results.RequiredN:F2}");

            const double T_ALPHA = 1.895;
            double intervalHalfWidth = T_ALPHA * results.Sigma / Math.Sqrt(results.TotalRuns);
            Console.WriteLine($"Доверительный интервал для среднего (90%): [{results.MeanWaitTime - intervalHalfWidth:F3}; {results.MeanWaitTime + intervalHalfWidth:F3}] мин.");
            Console.WriteLine($"Точность {(results.AccuracyAchieved ? "обеспечена" : "НЕ обеспечена")}");
        }

        static void PrintVariant2Results(AccuracyResults results)
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("РЕЗУЛЬТАТЫ МОДЕЛИРОВАНИЯ: ВАРИАНТ 2");
            Console.WriteLine(new string('=', 60));

            var sim = results.LastSimulation;
            if (sim != null)
            {
                Console.WriteLine($"Моделирование завершено по времени: 9 часов (540 мин.)");
                Console.WriteLine($"Всего вошло в систему: {sim.TotalCustomers}");
                Console.WriteLine($"Обслужено полностью: {sim.TotalCustomers}");

                // Вычисляем процент потерянных посетителей из-за отсутствия подносов
                int lostCustomers = Math.Abs(sim.TotalCustomers - (sim.FirstDishTaken + sim.SecondDishTaken + sim.SaladTaken));
                Console.WriteLine($"Потеряно из-за отсутствия подносов: {lostCustomers}");

                Console.WriteLine($"Среднее время пребывания в системе: {sim.AvgSystemTime:F2} мин.");
                Console.WriteLine($"Среднее время ожидания в очередях: {results.MeanWaitTime:F2} мин.");

                Console.WriteLine($"\nСТАТИСТИКА ПО БЛЮДАМ (ВАРИАНТ 2)");
                Console.WriteLine($"Получили первое блюдо: {sim.FirstDishTaken} ({(sim.FirstDishTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"Получили второе блюдо: {sim.SecondDishTaken} ({(sim.SecondDishTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"Получили салат: {sim.SaladTaken} ({(sim.SaladTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"Получили напиток: {sim.DrinkTaken} ({(sim.DrinkTaken * 100.0 / sim.TotalCustomers):F1}%)");
                Console.WriteLine($"Число людей, которым не понадобилось обслуживание: {sim.NoServiceNeeded}");

                Console.WriteLine($"Среднее время обслуживания: {sim.AvgServiceTime:F2} мин.");
                Console.WriteLine($"Среднее время получения обеда: {sim.AvgMealTime:F2} мин.");
                Console.WriteLine($"Загруженность кассы: {(sim.AvgServiceTime > 0 ? (sim.AvgServiceTime / sim.AvgSystemTime * 100) : 0):F1}% от времени в системе");

                Console.WriteLine($"\n--- Отказы ---");
                Console.WriteLine($"Отказов от первого блюда: {sim.RejectFirstMealResult}");
                Console.WriteLine($"Отказов от напитка: {sim.RejectDrinkResult}");
                Console.WriteLine($"Доля отказов от первого блюда: {sim.RejectFirstMealPercent:F1}%");
                Console.WriteLine($"Доля отказов от напитка: {sim.RejectDrinkPercent:F1}%");
            }

            // Вывод информации о точности
            Console.WriteLine($"\n--- ОБЕСПЕЧЕНИЕ ТОЧНОСТИ ---");
            Console.WriteLine($"Всего выполнено прогонов: {results.TotalRuns}");
            Console.WriteLine($"Среднее время ожидания: {results.MeanWaitTime:F2} мин.");
            Console.WriteLine($"Среднеквадратическое отклонение (σ): {results.Sigma:F3} мин.");
            Console.WriteLine($"Необходимое число реализаций (N*): {results.RequiredN:F2}");

            const double T_ALPHA = 1.895;
            double intervalHalfWidth = T_ALPHA * results.Sigma / Math.Sqrt(results.TotalRuns);
            Console.WriteLine($"Доверительный интервал для среднего (90%): [{results.MeanWaitTime - intervalHalfWidth:F3}; {results.MeanWaitTime + intervalHalfWidth:F3}] мин.");
            Console.WriteLine($"Точность {(results.AccuracyAchieved ? "обеспечена" : "НЕ обеспечена")}");
        }
    }
}