using BF_STT.Services.Infrastructure;
using BF_STT.Services.Workflow;
using System.Text.Json;
using Xunit;

namespace BF_STT.Tests
{
    public class MaxRecordingDurationTests
    {
        // ─── Limit check ──────────────────────────────────────────────────────

        [Fact]
        public void ExceedsMaxRecordingDuration_BelowLimit_ReturnsFalse()
        {
            Assert.False(RecordingCoordinator.ExceedsMaxRecordingDuration(TimeSpan.FromSeconds(599), 10));
        }

        [Fact]
        public void ExceedsMaxRecordingDuration_AtLimit_ReturnsTrue()
        {
            Assert.True(RecordingCoordinator.ExceedsMaxRecordingDuration(TimeSpan.FromMinutes(10), 10));
        }

        [Fact]
        public void ExceedsMaxRecordingDuration_LimitZero_NeverExceeds()
        {
            Assert.False(RecordingCoordinator.ExceedsMaxRecordingDuration(TimeSpan.FromHours(24), 0));
        }

        [Fact]
        public void ExceedsMaxRecordingDuration_NegativeLimit_NeverExceeds()
        {
            Assert.False(RecordingCoordinator.ExceedsMaxRecordingDuration(TimeSpan.FromHours(24), -5));
        }

        // ─── Settings defaults ────────────────────────────────────────────────

        [Fact]
        public void AppSettings_New_DefaultsToTenMinutes()
        {
            Assert.Equal(10, new AppSettings().MaxRecordingMinutes);
        }

        [Fact]
        public void AppSettings_JsonWithoutField_DefaultsToTenMinutes()
        {
            // settings.json written by older versions has no MaxRecordingMinutes.
            var settings = JsonSerializer.Deserialize<AppSettings>("{\"MaxHistoryItems\":50}");

            Assert.NotNull(settings);
            Assert.Equal(10, settings!.MaxRecordingMinutes);
        }
    }
}
