using System;
using System.Collections.Generic;
using System.Text;

namespace SessionSetupMicroServiceUnitTest.TestDoubles
{
    public class FixedClock : TimeProvider
    {
        public static readonly DateTimeOffset Default = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private DateTimeOffset _now;

        public FixedClock() : this(Default)
        {
        }

        public FixedClock(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }

        public void Advance(TimeSpan by)
        {
            _now = _now.Add(by);
        }
    }
}
