using Content.Server.Voting.Managers;
using Content.Shared.Voting;

// ReSharper disable CheckNamespace
namespace Content.Server.GameTicking;

public sealed partial class ServerGameTicker
{
    [Dependency] private IVoteManager _voteManager = default!;

    private void OrbitraStartLobbyVotes()
    {
        if (_playerManager.PlayerCount == 0)
            return;

        _voteManager.CreateStandardVote(null, StandardVoteType.Map);
        _voteManager.CreateStandardVote(null, StandardVoteType.Preset);
    }
}
