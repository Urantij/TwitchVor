using System.Linq;

namespace TwitchVor.Vvideo
{
    class VolumeTracker
    {
        public readonly DateTime trackStart;
        public readonly int GB;

        public VolumeTracker(DateTime trackStart, int GB)
        {
            this.trackStart = trackStart;
            this.GB = GB;
        }
    }

    /// <summary>
    /// Следит за стоимость стрима)
    /// </summary>
    class Pricer
    {
        //мы тут говорим о серьёзных цифрах, конечно decimal юзаем
        const decimal dropletCostPerHour = 0.007M;
        const decimal volumeCostPerGBPerHour = 0.00015M;

        const decimal taxesMult = 1.2M;

        readonly DateTime dropletTrackStart;
        readonly List<VolumeTracker> volumes = new();

        public Pricer(DateTime dropletTrackStart)
        {
            this.dropletTrackStart = dropletTrackStart;
        }

        public void AddVolume(DateTime volumeTrackStart, int GB)
        {
            //я уверен, этот лок не нужен. я уверен. я уверен.
            //но на всякий случай.......
            lock (volumes) volumes.Add(new VolumeTracker(volumeTrackStart, GB));
        }

        public decimal EstimateAll(DateTime date)
        {
            //TODO разобраться почему cost и почему price

            decimal result = 0M;

            result += EstimateThing(date, dropletTrackStart, dropletCostPerHour);

            //durka
            VolumeTracker[] volumes;
            lock (this.volumes) volumes = this.volumes.ToArray();

            foreach (var volume in volumes)
            {
                decimal costPerHour = volume.GB * volumeCostPerGBPerHour;

                result += EstimateThing(date, volume.trackStart, costPerHour);
            }

            return result * taxesMult;
        }

        private static decimal EstimateThing(DateTime now, DateTime trackStart, decimal costPerHour)
        {
            var passed = now - trackStart;

            return (decimal)Math.Ceiling(passed.TotalHours) * costPerHour;
        }
    }
}