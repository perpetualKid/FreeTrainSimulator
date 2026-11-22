using System;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Converts between typed native path-operation data and the current FTS/Open Rails-derived integer contract.
    /// This compatibility codec is not part of native model persistence.
    /// </summary>
    public static class PathWaitOperationCodec
    {
        private const int BasicWaitMaximum = 29999;

        public static bool TryCreateBasicWait(int waitSeconds, out PathNodeWaitInfo waitInfo)
        {
            waitInfo = null;
            if (waitSeconds is < 1 or > BasicWaitMaximum)
                return false;

            waitInfo = new PathNodeWaitInfo
            {
                WaitOperation = WaitOperationType.BasicWait,
                Duration = TimeSpan.FromSeconds(waitSeconds),
            };
            return true;
        }

        public static bool TryCreateWaitUntil(int hour, int minute, out PathNodeWaitInfo waitInfo)
        {
            waitInfo = null;
            if (hour is < 0 or > 23 || minute is < 0 or > 59)
                return false;

            waitInfo = new PathNodeWaitInfo
            {
                WaitOperation = WaitOperationType.WaitUntil,
                TimeOfDay = new TimeSpan(hour, minute, 0),
            };
            return true;
        }

        public static bool TryCreateUncouple(int carCount, int waitSeconds, bool keepRear, out PathNodeWaitInfo waitInfo)
        {
            waitInfo = null;
            if (carCount is < 1 or > 99 || waitSeconds is < 0 or > 99)
                return false;

            waitInfo = new PathNodeWaitInfo
            {
                WaitOperation = WaitOperationType.Uncouple,
                Duration = TimeSpan.FromSeconds(waitSeconds),
                CarCount = carCount,
                KeepRear = keepRear,
            };
            return true;
        }

        public static PathNodeWaitInfo JoinSplit() => new PathNodeWaitInfo
        {
            WaitOperation = WaitOperationType.JoinSplit,
        };

        public static PathNodeWaitInfo PassRedSignal() => new PathNodeWaitInfo
        {
            WaitOperation = WaitOperationType.PassRedSignal,
        };

        public static bool TryCreateHorn(int durationSeconds, out PathNodeWaitInfo waitInfo)
        {
            waitInfo = null;
            if (durationSeconds is < 1 or > 11)
                return false;

            waitInfo = new PathNodeWaitInfo
            {
                WaitOperation = WaitOperationType.Horn,
                Duration = TimeSpan.FromSeconds(durationSeconds),
            };
            return true;
        }

        public static PathNodeWaitInfo FromMstsWaitTime(int rawCode)
        {
            if (rawCode is >= 1 and <= BasicWaitMaximum)
                return new PathNodeWaitInfo
                {
                    WaitOperation = WaitOperationType.BasicWait,
                    Duration = TimeSpan.FromSeconds(rawCode),
                };

            if (rawCode is >= 30000 and < 40000)
            {
                int hour = (rawCode / 100) % 100;
                int minute = rawCode % 100;
                if (hour <= 23 && minute <= 59)
                {
                    return new PathNodeWaitInfo
                    {
                        WaitOperation = WaitOperationType.WaitUntil,
                        TimeOfDay = new TimeSpan(hour, minute, 0),
                    };
                }
            }

            if (rawCode is >= 40000 and < 50000)
                return CreateImportedUncouple(rawCode, false);
            if (rawCode is >= 50000 and < 60000)
                return CreateImportedUncouple(rawCode, true);
            if (rawCode == 60001)
                return JoinSplit();
            if (rawCode == 60002)
                return PassRedSignal();
            if (rawCode is >= 60011 and <= 60021)
            {
                return new PathNodeWaitInfo
                {
                    WaitOperation = WaitOperationType.Horn,
                    Duration = TimeSpan.FromSeconds(rawCode - 60010),
                };
            }

            return new PathNodeWaitInfo
            {
                WaitOperation = WaitOperationType.Unknown,
                UnknownCode = rawCode,
            };
        }

        public static bool IsValidForAuthoring(PathNodeWaitInfo waitInfo)
        {
            return waitInfo != null && waitInfo.UnknownCode == 0 && waitInfo.WaitOperation switch
            {
                WaitOperationType.BasicWait => waitInfo.TimeOfDay == TimeSpan.Zero && waitInfo.CarCount == 0
                    && !waitInfo.KeepRear && TryGetWholeSeconds(waitInfo.Duration, out int basicWait)
                    && basicWait is >= 1 and <= BasicWaitMaximum,
                WaitOperationType.WaitUntil => waitInfo.Duration == TimeSpan.Zero && waitInfo.CarCount == 0
                    && !waitInfo.KeepRear && IsExactTimeOfDay(waitInfo.TimeOfDay),
                WaitOperationType.Uncouple => waitInfo.TimeOfDay == TimeSpan.Zero && waitInfo.CarCount is >= 1 and <= 99
                    && TryGetWholeSeconds(waitInfo.Duration, out int uncoupleSeconds) && uncoupleSeconds is >= 0 and <= 99,
                WaitOperationType.JoinSplit or WaitOperationType.PassRedSignal => waitInfo.Duration == TimeSpan.Zero
                    && waitInfo.TimeOfDay == TimeSpan.Zero && waitInfo.CarCount == 0 && !waitInfo.KeepRear,
                WaitOperationType.Horn => waitInfo.TimeOfDay == TimeSpan.Zero && waitInfo.CarCount == 0
                    && !waitInfo.KeepRear && TryGetWholeSeconds(waitInfo.Duration, out int hornSeconds)
                    && hornSeconds is >= 1 and <= 11,
                _ => false,
            };
        }

        public static int? GetNullableLegacyWaitCode(PathNodeWaitInfo waitInfo) => waitInfo == null ? null : GetMstsWaitCode(waitInfo);

        public static int GetMstsWaitCode(PathNodeWaitInfo waitInfo)
        {
            return waitInfo == null || waitInfo.WaitOperation == WaitOperationType.None
                ? 0
                : waitInfo.WaitOperation switch
            {
                WaitOperationType.BasicWait when TryGetWholeSeconds(waitInfo.Duration, out int basicWait) => basicWait,
                WaitOperationType.WaitUntil when IsExactTimeOfDay(waitInfo.TimeOfDay) => 30000 + (100 * waitInfo.TimeOfDay.Hours) + waitInfo.TimeOfDay.Minutes,
                WaitOperationType.Uncouple when TryGetImportedUncoupleCode(waitInfo, out int uncoupleCode) => uncoupleCode,
                WaitOperationType.JoinSplit => 60001,
                WaitOperationType.PassRedSignal => 60002,
                WaitOperationType.Horn when TryGetWholeSeconds(waitInfo.Duration, out int hornSeconds) && hornSeconds is >= 1 and <= 11 => 60010 + hornSeconds,
                WaitOperationType.Unknown => waitInfo.UnknownCode,
                _ => 0,
            };
        }

        private static PathNodeWaitInfo CreateImportedUncouple(int rawCode, bool keepRear) => new PathNodeWaitInfo
        {
            WaitOperation = WaitOperationType.Uncouple,
            Duration = TimeSpan.FromSeconds(rawCode % 100),
            CarCount = (rawCode / 100) % 100,
            KeepRear = keepRear,
        };

        private static bool TryGetImportedUncoupleCode(PathNodeWaitInfo waitInfo, out int code)
        {
            code = 0;
            if (waitInfo.CarCount is < 0 or > 99 || !TryGetWholeSeconds(waitInfo.Duration, out int seconds) || seconds is < 0 or > 99)
                return false;

            code = (waitInfo.KeepRear ? 50000 : 40000) + (100 * waitInfo.CarCount) + seconds;
            return true;
        }

        private static bool IsExactTimeOfDay(TimeSpan value) => value >= TimeSpan.Zero
            && value < TimeSpan.FromDays(1) && value.Ticks % TimeSpan.TicksPerMinute == 0;

        private static bool TryGetWholeSeconds(TimeSpan value, out int seconds)
        {
            seconds = 0;
            if (value < TimeSpan.Zero || value > TimeSpan.FromSeconds(BasicWaitMaximum)
                || value.Ticks % TimeSpan.TicksPerSecond != 0)
                return false;

            seconds = (int)value.TotalSeconds;
            return true;
        }
    }
}
