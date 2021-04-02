
function selectGame(gameID) {
    $('#SteamGameID').val(gameID);
    $('#game-list').children().each(function () {
        var div = $(this);
        var id = div.children('.steam-game-id').val();
        if (id == gameID) {
            div.addClass('game-selected');
        }
        else {
            div.removeClass('game-selected');
        }
    });
}