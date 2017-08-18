using Discord;
using Discord.Commands;
using NadekoBot.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;
using NadekoBot.Common.Attributes;
using NadekoBot.Modules.Administration.Services;

namespace NadekoBot.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class PruneCommands : NadekoSubmodule<PruneService>
        {
            private static readonly ConcurrentDictionary<ulong, Timer> _autoPruneTimers = new ConcurrentDictionary<ulong, Timer>();
            private readonly TimeSpan twoWeeks = TimeSpan.FromDays(14);

            //delets her own messages, no perm required
            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Prune()
            {
                var user = await Context.Guild.GetCurrentUserAsync().ConfigureAwait(false);

                await _service.PruneWhere((ITextChannel)Context.Channel, 100, (x) => x.Author.Id == user.Id).ConfigureAwait(false);
                Context.Message.DeleteAfter(3);
            }
            // prune x
            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(ChannelPermission.ManageMessages)]
            [RequireBotPermission(GuildPermission.ManageMessages)]
            [Priority(1)]
            public async Task Prune(int count)
            {
                count++;
                if (count < 1)
                    return;
                if (count > 1000)
                    count = 1000;
                await _service.PruneWhere((ITextChannel)Context.Channel, count, x => true).ConfigureAwait(false);
            }

            //prune @user [x]
            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(ChannelPermission.ManageMessages)]
            [RequireBotPermission(GuildPermission.ManageMessages)]
            [Priority(0)]
            public async Task Prune(IGuildUser user, int count = 100)
            {
                if (user.Id == Context.User.Id)
                    count++;

                if (count < 1)
                    return;

                if (count > 1000)
                    count = 1000;
                await _service.PruneWhere((ITextChannel)Context.Channel, count, m => m.Author.Id == user.Id && DateTime.UtcNow - m.CreatedAt < twoWeeks);
            }

            #region Autoprune
            
            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(ChannelPermission.ManageMessages)]
            [RequireBotPermission(GuildPermission.ManageMessages)]
            [Priority(1)]
            [NadekoCommand, Usage, Description, Aliases]
            public async Task AutoPrune(int interval, int count = 100)
            {
                Timer t;

                if (interval == 0)
                {
                    if (!_autoPruneTimers.TryRemove(Context.Channel.Id, out t)) return;

                    t.Change(Timeout.Infinite, Timeout.Infinite); //proper way to disable the timer
                    await ReplyConfirmLocalized("autoprune_stopped").ConfigureAwait(false);
                    return;
                }

                if (interval < 20)
                    return;

                t = new Timer(async (state) =>
                {
                    try
                    {
                        await InternalPrune((ITextChannel)Context.Channel, count).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }, null, interval * 1000, interval * 1000);

                _autoPruneTimers.AddOrUpdate(Context.Channel.Id, t, (key, old) =>
                {
                    old.Change(Timeout.Infinite, Timeout.Infinite);
                    return t;
                });

                await ReplyConfirmLocalized("autoprune_started", interval).ConfigureAwait(false);
            }

            private async Task InternalPrune(ITextChannel Channel, int count)
            {
                count++;
                if (count < 1)
                    return;
                if (count > 1000)
                    count = 1000;
                await _service.PruneWhere(Channel, count, x => true).ConfigureAwait(false);
            }

            #endregion
        }
    }
}
