namespace XFramework.Integration.Services.Helpers
{
    public class StopWatchHelper
    {
        private DateTime _startTime;
        private DateTime _endTime;

        public void Start(string message = null)
        {
            _startTime = DateTime.Now;
            if (message != null) Console.WriteLine($"{message}");
        }
        
        public void Stop(string message)
        {
            _endTime = DateTime.Now;
            var elapsedTime = _endTime - _startTime;
            Console.WriteLine($"{message} in {elapsedTime.TotalMilliseconds}ms");
        }
    }
}