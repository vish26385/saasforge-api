//using SaaSForge.Api.Configurations;
//using SaaSForge.Api.Data;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Options;
//using Npgsql;

//namespace SaaSForge.Api.Services.Notifications
//{
//    public class NudgeWorker : BackgroundService
//    {
//        private readonly IServiceScopeFactory _scopeFactory;
//        private readonly ILogger<NudgeWorker> _logger;
//        private readonly IOptions<ExpoPushOptions> _opt;

//        // Strong types (NO dynamic)
//        private sealed record DueNudge(
//            int ItemId,
//            string UserId,
//            int TaskId,
//            string Label,
//            bool StartPending,
//            bool EndPending
//        );

//        private sealed record TokenRow(string UserId, string ExpoPushToken);

//        // ✅ Run frequently so nudges can be sent BEFORE NudgeAt / EndNudgeAtUtc
//        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

//        // ✅ Look a little ahead; if nudge is in next 15 seconds, send now
//        private static readonly TimeSpan LookAheadWindow = TimeSpan.FromSeconds(15);

//        public NudgeWorker(
//            IServiceScopeFactory scopeFactory,
//            ILogger<NudgeWorker> logger,
//            IOptions<ExpoPushOptions> opt)
//        {
//            _scopeFactory = scopeFactory;
//            _logger = logger;
//            _opt = opt;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            _logger.LogInformation("✅ NudgeWorker started.");

//            using var timer = new PeriodicTimer(TickInterval);

//            while (!stoppingToken.IsCancellationRequested)
//            {
//                try
//                {
//                    await RunOnce(stoppingToken);
//                }
//                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
//                {
//                    break;
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "❌ NudgeWorker loop error");
//                }

//                try
//                {
//                    await timer.WaitForNextTickAsync(stoppingToken);
//                }
//                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
//                {
//                    break;
//                }
//            }
//        }

//        private async Task RunOnce(CancellationToken ct)
//        {
//            using var scope = _scopeFactory.CreateScope();
//            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//            var push = scope.ServiceProvider.GetRequiredService<ExpoPushClient>();

//            var nowUtc = DateTime.UtcNow;
//            var lookAheadUtc = nowUtc.Add(LookAheadWindow);
//            var limit = 200;

//            // ✅ IMPORTANT:
//            // If a nudge time already passed, user does NOT want it later.
//            // So mark stale nudges as skipped and never send them afterwards.
//            await SafeExecuteUpdate(() => SkipStaleStartNudges(db, nowUtc, ct));
//            await SafeExecuteUpdate(() => SkipStaleEndNudges(db, nowUtc, ct));

//            // ✅ Pull ONLY upcoming nudges inside look-ahead window.
//            // This makes notifications go BEFORE NudgeAt / EndNudgeAtUtc passes.
//            var due = await SafeDbQuery(async () =>
//            {
//                return await (
//                    from i in db.DailyPlanItems.AsNoTracking()
//                    join p in db.DailyPlans.AsNoTracking() on i.PlanId equals p.Id
//                    where i.TaskId != null
//                          && (
//                                (i.NudgeAt != null
//                                 && i.NudgeAt > nowUtc
//                                 && i.NudgeAt <= lookAheadUtc
//                                 && i.NudgeSentAtUtc == null
//                                 && i.Start > nowUtc)
//                             ||
//                                (i.EndNudgeAtUtc != null
//                                 && i.EndNudgeAtUtc > nowUtc
//                                 && i.EndNudgeAtUtc <= lookAheadUtc
//                                 && i.EndNudgeSentAtUtc == null
//                                 && i.End > nowUtc)
//                             )
//                    orderby (i.NudgeAt ?? i.EndNudgeAtUtc)
//                    select new DueNudge(
//                        i.Id,
//                        p.UserId,
//                        i.TaskId!.Value,
//                        i.Label,
//                        i.NudgeAt != null
//                            && i.NudgeAt > nowUtc
//                            && i.NudgeAt <= lookAheadUtc
//                            && i.NudgeSentAtUtc == null
//                            && i.Start > nowUtc,
//                        i.EndNudgeAtUtc != null
//                            && i.EndNudgeAtUtc > nowUtc
//                            && i.EndNudgeAtUtc <= lookAheadUtc
//                            && i.EndNudgeSentAtUtc == null
//                            && i.End > nowUtc
//                    )
//                )
//                .Take(limit)
//                .ToListAsync(ct);
//            }, fallback: new List<DueNudge>());

//            if (due.Count == 0) return;

//            _logger.LogInformation(
//                "🔔 NudgeWorker found {Count} upcoming nudges in next {Seconds} seconds.",
//                due.Count,
//                (int)LookAheadWindow.TotalSeconds);

//            // ✅ Load device tokens for all users involved (read-only)
//            var userIds = due.Select(x => x.UserId).Distinct().ToList();

//            var tokens = await SafeDbQuery(async () =>
//            {
//                return await db.UserDeviceTokens
//                    .AsNoTracking()
//                    .Where(x => x.IsActive && userIds.Contains(x.UserId))
//                    .Select(x => new TokenRow(x.UserId, x.ExpoPushToken))
//                    .ToListAsync(ct);
//            }, fallback: new List<TokenRow>());

//            var tokensByUser = tokens
//                .GroupBy(t => t.UserId)
//                .ToDictionary(g => g.Key, g => g.Select(x => x.ExpoPushToken).Distinct().ToList());

//            var options = _opt.Value ?? new ExpoPushOptions();
//            var batchSize = Math.Max(1, options.BatchSize);

//            foreach (var d in due)
//            {
//                ct.ThrowIfCancellationRequested();

//                if (!tokensByUser.TryGetValue(d.UserId, out var userTokens) || userTokens.Count == 0)
//                {
//                    await SafeExecuteUpdate(
//                        () => SafeSetLastError(db, d.ItemId, "No active device tokens for user.", ct));
//                    continue;
//                }

//                // ✅ START nudge (claim first, then send)
//                if (d.StartPending)
//                {
//                    var claimed = await SafeExecuteUpdateBool(
//                        () => ClaimStartNudge(db, d.ItemId, nowUtc, lookAheadUtc, ct));

//                    if (claimed)
//                    {
//                        var title = "⏰ Task starting soon";
//                        var body = $"{d.Label} starts in 5 minutes.";

//                        var messages = userTokens.Select(tok => new ExpoPushMessage
//                        {
//                            To = tok,
//                            Title = title,
//                            Body = body,
//                            Data = new Dictionary<string, object>
//                            {
//                                ["planItemId"] = d.ItemId,
//                                ["taskId"] = d.TaskId,
//                                ["type"] = "plan_start_nudge"
//                            }
//                        });

//                        var (anyOk, lastError) = await SendBatches(push, messages, batchSize, ct);

//                        if (anyOk)
//                        {
//                            await SafeExecuteUpdate(() => ClearLastError(db, d.ItemId, ct));
//                        }
//                        else
//                        {
//                            await SafeExecuteUpdate(() =>
//                                RevertStartClaim(db, d.ItemId, lastError ?? "Unknown Expo error", ct));
//                        }
//                    }
//                }

//                // ✅ END nudge (claim first, then send)
//                if (d.EndPending)
//                {
//                    var claimed = await SafeExecuteUpdateBool(
//                        () => ClaimEndNudge(db, d.ItemId, nowUtc, lookAheadUtc, ct));

//                    if (claimed)
//                    {
//                        var title = "✅ Task ending soon";
//                        var body = $"{d.Label} ends in 5 minutes.";

//                        var messages = userTokens.Select(tok => new ExpoPushMessage
//                        {
//                            To = tok,
//                            Title = title,
//                            Body = body,
//                            Data = new Dictionary<string, object>
//                            {
//                                ["planItemId"] = d.ItemId,
//                                ["taskId"] = d.TaskId,
//                                ["type"] = "plan_end_nudge"
//                            }
//                        });

//                        var (anyOk, lastError) = await SendBatches(push, messages, batchSize, ct);

//                        if (anyOk)
//                        {
//                            await SafeExecuteUpdate(() => ClearLastError(db, d.ItemId, ct));
//                        }
//                        else
//                        {
//                            await SafeExecuteUpdate(() =>
//                                RevertEndClaim(db, d.ItemId, lastError ?? "Unknown Expo error", ct));
//                        }
//                    }
//                }
//            }
//        }

//        // ----------------- DB SAFETY WRAPPERS -----------------

//        private async Task<T> SafeDbQuery<T>(Func<Task<T>> action, T fallback)
//        {
//            try
//            {
//                return await action();
//            }
//            catch (PostgresException ex) when (ex.SqlState == "40P01" || ex.SqlState == "40001")
//            {
//                _logger.LogWarning(ex, "⚠️ DB transient error (deadlock/serialization) during query. Skipping this tick.");
//                return fallback;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "⚠️ DB query failed. Skipping this tick.");
//                return fallback;
//            }
//        }

//        private async Task SafeExecuteUpdate(Func<Task> updateAction)
//        {
//            try
//            {
//                await updateAction();
//            }
//            catch (PostgresException ex) when (ex.SqlState == "40P01" || ex.SqlState == "40001")
//            {
//                _logger.LogWarning(ex, "⚠️ DB transient error (deadlock/serialization) during update. Will retry next tick.");
//            }
//            catch (DbUpdateConcurrencyException ex)
//            {
//                _logger.LogWarning(ex, "⚠️ Concurrency during update (row may be deleted). Ignoring.");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "⚠️ Update failed. Ignoring for this tick.");
//            }
//        }

//        private async Task<bool> SafeExecuteUpdateBool(Func<Task<bool>> updateAction)
//        {
//            try
//            {
//                return await updateAction();
//            }
//            catch (PostgresException ex) when (ex.SqlState == "40P01" || ex.SqlState == "40001")
//            {
//                _logger.LogWarning(ex, "⚠️ DB transient error (deadlock/serialization) during update. Will retry next tick.");
//                return false;
//            }
//            catch (DbUpdateConcurrencyException ex)
//            {
//                _logger.LogWarning(ex, "⚠️ Concurrency during update (row may be deleted). Ignoring.");
//                return false;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "⚠️ Update failed. Ignoring for this tick.");
//                return false;
//            }
//        }

//        // ----------------- STALE SKIP HELPERS -----------------

//        private static async Task SkipStaleStartNudges(AppDbContext db, DateTime nowUtc, CancellationToken ct)
//        {
//            await db.DailyPlanItems
//                .Where(i =>
//                    i.TaskId != null
//                    && i.NudgeAt != null
//                    && i.NudgeAt <= nowUtc
//                    && i.NudgeSentAtUtc == null)
//                .ExecuteUpdateAsync(setters => setters
//                    .SetProperty(i => i.NudgeSentAtUtc, nowUtc)
//                    .SetProperty(i => i.LastNudgeError, "Skipped stale start nudge because nudge time already passed."),
//                    ct);
//        }

//        private static async Task SkipStaleEndNudges(AppDbContext db, DateTime nowUtc, CancellationToken ct)
//        {
//            await db.DailyPlanItems
//                .Where(i =>
//                    i.TaskId != null
//                    && i.EndNudgeAtUtc != null
//                    && i.EndNudgeAtUtc <= nowUtc
//                    && i.EndNudgeSentAtUtc == null)
//                .ExecuteUpdateAsync(setters => setters
//                    .SetProperty(i => i.EndNudgeSentAtUtc, nowUtc)
//                    .SetProperty(i => i.LastNudgeError, "Skipped stale end nudge because nudge time already passed."),
//                    ct);
//        }

//        // ----------------- CLAIM HELPERS (atomic, short locks) -----------------

//        private static async Task<bool> ClaimStartNudge(
//            AppDbContext db,
//            int itemId,
//            DateTime nowUtc,
//            DateTime lookAheadUtc,
//            CancellationToken ct)
//        {
//            var rows = await db.DailyPlanItems
//                .Where(i =>
//                    i.Id == itemId
//                    && i.TaskId != null
//                    && i.NudgeAt != null
//                    && i.NudgeAt > nowUtc
//                    && i.NudgeAt <= lookAheadUtc
//                    && i.NudgeSentAtUtc == null
//                    && i.Start > nowUtc)
//                .ExecuteUpdateAsync(setters => setters
//                    .SetProperty(i => i.NudgeSentAtUtc, nowUtc)
//                    .SetProperty(i => i.LastNudgeError, (string?)null),
//                    ct);

//            return rows == 1;
//        }

//        private static async Task<bool> ClaimEndNudge(
//            AppDbContext db,
//            int itemId,
//            DateTime nowUtc,
//            DateTime lookAheadUtc,
//            CancellationToken ct)
//        {
//            var rows = await db.DailyPlanItems
//                .Where(i =>
//                    i.Id == itemId
//                    && i.TaskId != null
//                    && i.EndNudgeAtUtc != null
//                    && i.EndNudgeAtUtc > nowUtc
//                    && i.EndNudgeAtUtc <= lookAheadUtc
//                    && i.EndNudgeSentAtUtc == null
//                    && i.End > nowUtc)
//                .ExecuteUpdateAsync(setters => setters
//                    .SetProperty(i => i.EndNudgeSentAtUtc, nowUtc)
//                    .SetProperty(i => i.LastNudgeError, (string?)null),
//                    ct);

//            return rows == 1;
//        }

//        private static async Task RevertStartClaim(AppDbContext db, int itemId, string error, CancellationToken ct)
//        {
//            await db.DailyPlanItems
//                .Where(i => i.Id == itemId && i.NudgeSentAtUtc != null)
//                .ExecuteUpdateAsync(setters => setters
//                    .SetProperty(i => i.NudgeSentAtUtc, (DateTime?)null)
//                    .SetProperty(i => i.LastNudgeError, error),
//                    ct);
//        }

//        private static async Task RevertEndClaim(AppDbContext db, int itemId, string error, CancellationToken ct)
//        {
//            await db.DailyPlanItems
//                .Where(i => i.Id == itemId && i.EndNudgeSentAtUtc != null)
//                .ExecuteUpdateAsync(setters => setters
//                    .SetProperty(i => i.EndNudgeSentAtUtc, (DateTime?)null)
//                    .SetProperty(i => i.LastNudgeError, error),
//                    ct);
//        }

//        private static async Task SafeSetLastError(AppDbContext db, int itemId, string error, CancellationToken ct)
//        {
//            await db.DailyPlanItems
//                .Where(i => i.Id == itemId)
//                .ExecuteUpdateAsync(setters => setters
//                    .SetProperty(i => i.LastNudgeError, error),
//                    ct);
//        }

//        private static async Task ClearLastError(AppDbContext db, int itemId, CancellationToken ct)
//        {
//            await db.DailyPlanItems
//                .Where(i => i.Id == itemId)
//                .ExecuteUpdateAsync(setters => setters
//                    .SetProperty(i => i.LastNudgeError, (string?)null),
//                    ct);
//        }

//        // ----------------- EXPO SEND -----------------

//        private static async Task<(bool anyOk, string? lastError)> SendBatches(
//            ExpoPushClient push,
//            IEnumerable<ExpoPushMessage> messages,
//            int batchSize,
//            CancellationToken ct)
//        {
//            bool anyOk = false;
//            string? lastError = null;

//            foreach (var batch in messages.Chunk(batchSize))
//            {
//                (bool ok, string? error) = await push.SendAsync(batch, ct);

//                if (ok) anyOk = true;
//                if (!ok) lastError = error;
//            }

//            return (anyOk, lastError);
//        }
//    }
//}

using SaaSForge.Api.Configurations;
using SaaSForge.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace SaaSForge.Api._LegacyFlowOS.Services.Notifications
{
    public class NudgeWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NudgeWorker> _logger;
        private readonly IOptions<ExpoPushOptions> _opt;

        private sealed record DueNudge(
            int ItemId,
            string UserId,
            int TaskId,
            string Label,
            bool StartPending,
            bool EndPending
        );

        private sealed record TokenRow(string UserId, string ExpoPushToken);

        // ✅ run frequently
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

        // ✅ send slightly before due time
        private static readonly TimeSpan LookAheadWindow = TimeSpan.FromSeconds(15);

        // ✅ IMPORTANT: allow a small late grace so nudges are not missed
        // if worker wakes a few seconds late
        private static readonly TimeSpan SendGraceWindow = TimeSpan.FromSeconds(90);

        public NudgeWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<NudgeWorker> logger,
            IOptions<ExpoPushOptions> opt)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _opt = opt;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ NudgeWorker started.");

            using var timer = new PeriodicTimer(TickInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnce(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ NudgeWorker loop error");
                }

                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task RunOnce(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var push = scope.ServiceProvider.GetRequiredService<ExpoPushClient>();

            var nowUtc = DateTime.UtcNow;
            var lookAheadUtc = nowUtc.Add(LookAheadWindow);
            var graceStartUtc = nowUtc.Subtract(SendGraceWindow);
            var limit = 200;

            // ✅ Skip only VERY OLD missed nudges.
            // Do NOT skip nudges that are just a few seconds late.
            await SafeExecuteUpdate(() => SkipVeryOldStartNudges(db, graceStartUtc, ct));
            await SafeExecuteUpdate(() => SkipVeryOldEndNudges(db, graceStartUtc, ct));

            // ✅ pull nudges in [now - grace, now + lookAhead]
            var due = await SafeDbQuery(async () =>
            {
                return await (
                    from i in db.DailyPlanItems.AsNoTracking()
                    join p in db.DailyPlans.AsNoTracking() on i.PlanId equals p.Id
                    where i.TaskId != null
                          && (
                                i.NudgeAt != null
                                 && i.NudgeAt >= graceStartUtc
                                 && i.NudgeAt <= lookAheadUtc
                                 && i.NudgeSentAtUtc == null
                                 && i.Start > nowUtc
                             ||
                                i.EndNudgeAtUtc != null
                                 && i.EndNudgeAtUtc >= graceStartUtc
                                 && i.EndNudgeAtUtc <= lookAheadUtc
                                 && i.EndNudgeSentAtUtc == null
                                 && i.End > nowUtc
                             )
                    orderby (i.NudgeAt ?? i.EndNudgeAtUtc)
                    select new DueNudge(
                        i.Id,
                        p.UserId,
                        i.TaskId!.Value,
                        i.Label,
                        i.NudgeAt != null
                            && i.NudgeAt >= graceStartUtc
                            && i.NudgeAt <= lookAheadUtc
                            && i.NudgeSentAtUtc == null
                            && i.Start > nowUtc,
                        i.EndNudgeAtUtc != null
                            && i.EndNudgeAtUtc >= graceStartUtc
                            && i.EndNudgeAtUtc <= lookAheadUtc
                            && i.EndNudgeSentAtUtc == null
                            && i.End > nowUtc
                    )
                )
                .Take(limit)
                .ToListAsync(ct);
            }, fallback: new List<DueNudge>());

            if (due.Count == 0) return;

            _logger.LogInformation(
                "🔔 NudgeWorker found {Count} nudges in send window. Now={NowUtc}, GraceStart={GraceStartUtc}, LookAhead={LookAheadUtc}",
                due.Count,
                nowUtc,
                graceStartUtc,
                lookAheadUtc);

            var userIds = due.Select(x => x.UserId).Distinct().ToList();

            var tokens = await SafeDbQuery(async () =>
            {
                return await db.UserDeviceTokens
                    .AsNoTracking()
                    .Where(x => x.IsActive && userIds.Contains(x.UserId))
                    .Select(x => new TokenRow(x.UserId, x.ExpoPushToken))
                    .ToListAsync(ct);
            }, fallback: new List<TokenRow>());

            var tokensByUser = tokens
                .GroupBy(t => t.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ExpoPushToken).Distinct().ToList());

            var options = _opt.Value ?? new ExpoPushOptions();
            var batchSize = Math.Max(1, options.BatchSize);

            foreach (var d in due)
            {
                ct.ThrowIfCancellationRequested();

                if (!tokensByUser.TryGetValue(d.UserId, out var userTokens) || userTokens.Count == 0)
                {
                    await SafeExecuteUpdate(
                        () => SafeSetLastError(db, d.ItemId, "No active device tokens for user.", ct));
                    continue;
                }

                // ✅ START nudge
                if (d.StartPending)
                {
                    var claimed = await SafeExecuteUpdateBool(
                        () => ClaimStartNudge(db, d.ItemId, nowUtc, graceStartUtc, lookAheadUtc, ct));

                    if (claimed)
                    {
                        var title = "⏰ Task starting soon";
                        var body = $"{d.Label} starts in 5 minutes.";

                        var messages = userTokens.Select(tok => new ExpoPushMessage
                        {
                            To = tok,
                            Title = title,
                            Body = body,
                            Data = new Dictionary<string, object>
                            {
                                ["planItemId"] = d.ItemId,
                                ["taskId"] = d.TaskId,
                                ["type"] = "plan_start_nudge"
                            }
                        });

                        var (anyOk, lastError) = await SendBatches(push, messages, batchSize, ct);

                        if (anyOk)
                        {
                            await SafeExecuteUpdate(() => ClearLastError(db, d.ItemId, ct));
                        }
                        else
                        {
                            await SafeExecuteUpdate(() =>
                                RevertStartClaim(db, d.ItemId, lastError ?? "Unknown Expo error", ct));
                        }
                    }
                }

                // ✅ END nudge
                if (d.EndPending)
                {
                    var claimed = await SafeExecuteUpdateBool(
                        () => ClaimEndNudge(db, d.ItemId, nowUtc, graceStartUtc, lookAheadUtc, ct));

                    if (claimed)
                    {
                        var title = "✅ Task ending soon";
                        var body = $"{d.Label} ends in 5 minutes.";

                        var messages = userTokens.Select(tok => new ExpoPushMessage
                        {
                            To = tok,
                            Title = title,
                            Body = body,
                            Data = new Dictionary<string, object>
                            {
                                ["planItemId"] = d.ItemId,
                                ["taskId"] = d.TaskId,
                                ["type"] = "plan_end_nudge"
                            }
                        });

                        var (anyOk, lastError) = await SendBatches(push, messages, batchSize, ct);

                        if (anyOk)
                        {
                            await SafeExecuteUpdate(() => ClearLastError(db, d.ItemId, ct));
                        }
                        else
                        {
                            await SafeExecuteUpdate(() =>
                                RevertEndClaim(db, d.ItemId, lastError ?? "Unknown Expo error", ct));
                        }
                    }
                }
            }
        }

        // ----------------- DB SAFETY WRAPPERS -----------------

        private async Task<T> SafeDbQuery<T>(Func<Task<T>> action, T fallback)
        {
            try
            {
                return await action();
            }
            catch (PostgresException ex) when (ex.SqlState == "40P01" || ex.SqlState == "40001")
            {
                _logger.LogWarning(ex, "⚠️ DB transient error (deadlock/serialization) during query. Skipping this tick.");
                return fallback;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ DB query failed. Skipping this tick.");
                return fallback;
            }
        }

        private async Task SafeExecuteUpdate(Func<Task> updateAction)
        {
            try
            {
                await updateAction();
            }
            catch (PostgresException ex) when (ex.SqlState == "40P01" || ex.SqlState == "40001")
            {
                _logger.LogWarning(ex, "⚠️ DB transient error (deadlock/serialization) during update. Will retry next tick.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "⚠️ Concurrency during update (row may be deleted). Ignoring.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Update failed. Ignoring for this tick.");
            }
        }

        private async Task<bool> SafeExecuteUpdateBool(Func<Task<bool>> updateAction)
        {
            try
            {
                return await updateAction();
            }
            catch (PostgresException ex) when (ex.SqlState == "40P01" || ex.SqlState == "40001")
            {
                _logger.LogWarning(ex, "⚠️ DB transient error (deadlock/serialization) during update. Will retry next tick.");
                return false;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "⚠️ Concurrency during update (row may be deleted). Ignoring.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Update failed. Ignoring for this tick.");
                return false;
            }
        }

        // ----------------- STALE SKIP HELPERS -----------------

        // ✅ skip only very old missed nudges (older than grace window)
        private static async Task SkipVeryOldStartNudges(AppDbContext db, DateTime graceStartUtc, CancellationToken ct)
        {
            await db.DailyPlanItems
                .Where(i =>
                    i.TaskId != null
                    && i.NudgeAt != null
                    && i.NudgeAt < graceStartUtc
                    && i.NudgeSentAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.NudgeSentAtUtc, DateTime.UtcNow)
                    .SetProperty(i => i.LastNudgeError, "Skipped stale start nudge because send window already passed."),
                    ct);
        }

        private static async Task SkipVeryOldEndNudges(AppDbContext db, DateTime graceStartUtc, CancellationToken ct)
        {
            await db.DailyPlanItems
                .Where(i =>
                    i.TaskId != null
                    && i.EndNudgeAtUtc != null
                    && i.EndNudgeAtUtc < graceStartUtc
                    && i.EndNudgeSentAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.EndNudgeSentAtUtc, DateTime.UtcNow)
                    .SetProperty(i => i.LastNudgeError, "Skipped stale end nudge because send window already passed."),
                    ct);
        }

        // ----------------- CLAIM HELPERS -----------------

        private static async Task<bool> ClaimStartNudge(
            AppDbContext db,
            int itemId,
            DateTime nowUtc,
            DateTime graceStartUtc,
            DateTime lookAheadUtc,
            CancellationToken ct)
        {
            var rows = await db.DailyPlanItems
                .Where(i =>
                    i.Id == itemId
                    && i.TaskId != null
                    && i.NudgeAt != null
                    && i.NudgeAt >= graceStartUtc
                    && i.NudgeAt <= lookAheadUtc
                    && i.NudgeSentAtUtc == null
                    && i.Start > nowUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.NudgeSentAtUtc, nowUtc)
                    .SetProperty(i => i.LastNudgeError, (string?)null),
                    ct);

            return rows == 1;
        }

        private static async Task<bool> ClaimEndNudge(
            AppDbContext db,
            int itemId,
            DateTime nowUtc,
            DateTime graceStartUtc,
            DateTime lookAheadUtc,
            CancellationToken ct)
        {
            var rows = await db.DailyPlanItems
                .Where(i =>
                    i.Id == itemId
                    && i.TaskId != null
                    && i.EndNudgeAtUtc != null
                    && i.EndNudgeAtUtc >= graceStartUtc
                    && i.EndNudgeAtUtc <= lookAheadUtc
                    && i.EndNudgeSentAtUtc == null
                    && i.End > nowUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.EndNudgeSentAtUtc, nowUtc)
                    .SetProperty(i => i.LastNudgeError, (string?)null),
                    ct);

            return rows == 1;
        }

        private static async Task RevertStartClaim(AppDbContext db, int itemId, string error, CancellationToken ct)
        {
            await db.DailyPlanItems
                .Where(i => i.Id == itemId && i.NudgeSentAtUtc != null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.NudgeSentAtUtc, (DateTime?)null)
                    .SetProperty(i => i.LastNudgeError, error),
                    ct);
        }

        private static async Task RevertEndClaim(AppDbContext db, int itemId, string error, CancellationToken ct)
        {
            await db.DailyPlanItems
                .Where(i => i.Id == itemId && i.EndNudgeSentAtUtc != null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.EndNudgeSentAtUtc, (DateTime?)null)
                    .SetProperty(i => i.LastNudgeError, error),
                    ct);
        }

        private static async Task SafeSetLastError(AppDbContext db, int itemId, string error, CancellationToken ct)
        {
            await db.DailyPlanItems
                .Where(i => i.Id == itemId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.LastNudgeError, error),
                    ct);
        }

        private static async Task ClearLastError(AppDbContext db, int itemId, CancellationToken ct)
        {
            await db.DailyPlanItems
                .Where(i => i.Id == itemId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.LastNudgeError, (string?)null),
                    ct);
        }

        // ----------------- EXPO SEND -----------------

        private static async Task<(bool anyOk, string? lastError)> SendBatches(
            ExpoPushClient push,
            IEnumerable<ExpoPushMessage> messages,
            int batchSize,
            CancellationToken ct)
        {
            bool anyOk = false;
            string? lastError = null;

            foreach (var batch in messages.Chunk(batchSize))
            {
                (bool ok, string? error) = await push.SendAsync(batch, ct);

                if (ok) anyOk = true;
                if (!ok) lastError = error;
            }

            return (anyOk, lastError);
        }
    }
}