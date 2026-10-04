using Content.Client._Orbitra.Voting;
using Content.Client.Lobby.UI;
using Content.Shared.Voting;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;

namespace Content.Client.Voting;

public sealed partial class VoteManager
{
    private OrbitraMapVoteWindow? _orbitraMapVoteWindow;

    private bool IsOrbitraMapVote(ActiveVote vote)
    {
        return (vote.Title == Loc.GetString("ui-vote-map-title") ||
                vote.Title is "Next map" or "Следующая карта") &&
               IoCManager.Resolve<IUserInterfaceManager>().ActiveScreen is LobbyGui;
    }

    private void UpdateOrbitraMapVote(ActiveVote vote)
    {
        if (!IsOrbitraMapVote(vote))
            return;

        if (_orbitraMapVoteWindow == null || _orbitraMapVoteWindow.Disposed || !_orbitraMapVoteWindow.IsOpen)
        {
            _orbitraMapVoteWindow = new OrbitraMapVoteWindow(vote);
            _orbitraMapVoteWindow.SetVoteId(vote.Id);
            _orbitraMapVoteWindow.OpenCentered();
        }
        else
        {
            _orbitraMapVoteWindow.UpdateData(vote);
        }
    }

    private void CloseOrbitraMapVote()
    {
        if (_orbitraMapVoteWindow == null)
            return;

        _orbitraMapVoteWindow.Close();
        _orbitraMapVoteWindow.Dispose();
        _orbitraMapVoteWindow = null;
    }
}
