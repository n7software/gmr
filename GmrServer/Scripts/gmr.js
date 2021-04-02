var notificationsClosedUrl;
var lockCP = false;
var extraIDs = new Object();
var runtimeVersion = "4.0.0";
var checkClient = false;
var directLink = "ClientDeploy/GmrClient.application";
var lastGameDetailsId = '';
var lastMessageDetailsId = '';
var lastPlayerDetailsId = '';
var defaultTab = "#tab-public-games";
var defaultTabContent = "#public-games";
var isUserAuthenticated = false;
var myGamesLoaded = false;
var publicGamesLoaded = false;
var playersLoaded = false;
var turnsLoaded = false;
var playersDelayTime = 300;
var messagesLoaded = false;
var emailSaved = false;
var emailDeleted = false;

var SavingFlags = {
    SavingInProgressSelection: false,
    SavingNewSelection: false,
    SavingSortMethod: false,

    IsSavingPreferences: function () {
        return this.SavingInProgressSelection
            || this.SavingNewSelection
            || this.SavingSortMethod;
    }
};

var pagedPlayerList = {
    totalPages: 0,
    currentPage: 0,
    currentColumn: '',
    queryUrl: "/Community/PlayersPage",
    userPageQueryUrl: "/Community/FindCurrentPlayerPageNumber",
    userPage: -1,
    findPlayerName: "",
    pageCache: {},
    columnSort: {}
};

var columnNames = [];

var recipients = [];

var playerToSendMessage = null;

function removeA(arr) {
    var what, a = arguments, L = a.length, ax;
    while (L > 1 && arr.length) {
        what = a[--L];
        while ((ax = arr.indexOf(what)) !== -1) {
            arr.splice(ax, 1);
        }
    }
    return arr;
}

function isDefined(func) {
    if (func !== undefined) return true;
}

$(function () {
    if (!Array.prototype.indexOf) {
        Array.prototype.indexOf = function (what, i) {
            i = i || 0;
            var L = this.length;
            while (i < L) {
                if (this[i] === what) return i;
                ++i;
            }
            return -1;
        };
    }
});

function evalHtmlAndScript(data) {
    var dom = $(data);

    dom.filter('script').each(function () {
        $.globalEval(this.text || this.textContent || this.innerHTML || '');
    });

    return dom.html();
}

function setNotificationsClosedUrl(value) {
    notificationsClosedUrl = value;
}

function makeUnselectable(node) {
    if (node.nodeType == 1) {
        node.unselectable = true;
    }
    var child = node.firstChild;
    while (child) {
        makeUnselectable(child);
        child = child.nextSibling;
    }
}

$(document).ready(function () {
    $('#notification-popup').click(function (e) {
        e.stopPropagation();
    });
    $('#cp-popup').click(function (e) {
        e.stopPropagation();
    });
    $('#notification-popup').hide();
    $('#cp-popup').hide();
    
    refreshUsersAndNotifications();
    
    window.setInterval(
        function () {
            refreshUsersAndNotifications();
        }, 30000);
});

$(document).click(function () {

    if ($('#notification-popup').is(':visible')) {
        $.post(notificationsClosedUrl, null, function () { }, 'html');
    }

    $('#notification-popup').hide(100);
    if (!lockCP) {
        $('#cp-popup').hide(100);
    }
    $('#selector-popup-wrapper').hide(100);
});

function watermark(sender) {
    var jqobj = $('#' + sender.id);
    if (jqobj.hasClass("watermark")) {
        sender.value = '';
        jqobj.removeClass("watermark");
    }
}

function refreshUsersAndNotifications() {
    queryUrl = '/Query/AvatarAndNotifications?extraUsers=';
    
    for (var id in extraIDs) {
        queryUrl = queryUrl + id.toString() + ",";
    }

    $.ajax({
        type: "GET",
        url: queryUrl,
        success: function (data) {
            if (data.sessionExpired) {
                window.location.reload();
            }
            else {
                $("#avatar").attr("src", data.avatarBorder);

                for (i in data.otherUsers) {
                    var user = data.otherUsers[i];
                    updateAvatarBorders(user);
                }

                updateNotificationsCount(data);
                updateMessagesCount(data);
            }
        },
        dataType: "json",
        cache: false
    });
}

function updateNotificationsCount(data) {
    var notnum = $('#notification-num');
    if (data.notifications > 0 && !notnum.hasClass('yes-notification')) {
        notnum.removeClass('no-notification');
        notnum.addClass('yes-notification');
    }
    else if (data.notifications == 0 && !notnum.hasClass('no-notification')) {
        notnum.removeClass('yes-notification');
        notnum.addClass('no-notification');
    }
    notnum.html(data.notifications);
}

function updateMessagesCount(data) {
    var notnum = $('#messages-num');
    if (data.messages > 0 && !notnum.hasClass('yes-notification')) {
        notnum.removeClass('no-notification');
        notnum.addClass('yes-notification');
    }
    else if (data.messages == 0 && !notnum.hasClass('no-notification')) {
        notnum.removeClass('yes-notification');
        notnum.addClass('no-notification');
    }
    notnum.html(data.messages);
}

function updateAvatarBorders(user) {
    $("." + user.id).attr("src", user.avatarBorder);
    $("." + user.id + "sm").attr("src", user.smallAvatarBorder);
    $("." + user.id + "lg").attr("src", user.largeAvatarBorder);
}

function requestUserInfo(userId) {
    extraIDs[userId] = true;
}

function selectTab(tabId, contentId) {
    $("li.selected").removeClass("selected");
    $(tabId).addClass("selected");

    showTabContent(contentId);
}

function showTabContent(contentId) {
    $("div.tabcontent").css("display", "none");
    $(contentId).css("display", "block");
}

function selectSubTab(containerId, tab) {
    $(containerId + ' .sub-tab-header .selected').removeClass('selected');
    $(tab + '-tab').addClass('selected');

    showSubTabContent(containerId, tab);
}

function showSubTabContent(containerId, tab) {
    $(containerId + ' .sub-tab-container .sub-tab-content').css('display', 'none');
    $(tab + '-content').css('display', 'block');
}

function AddTooltip(obj) {
    
    obj.qtip({
        position: {
            target: 'mouse',
            adjust: {
                x: 5, y: 5
            }
        },
        hide: {
            fixed: true // Helps to prevent the tooltip from hiding ocassionally when tracking!
        },
        style: {
            widget: false,
            def: false,
            classes: 'qtip-style'
        }
    });

    if (obj.hasClass('unparsed-tooltip')) {
        obj.removeClass('unparsed-tooltip');
    }
}

function AddGameTooltip(objects) {
    objects.each(setGameTooltip);
}

function setGameTooltip(index, element) {
    var obj = $(element);

    obj.qtip({
        content: getGameTooltipContent(obj),
        position: {
            my: 'top left',
            at: 'top left',
            target: obj,
            adjust: {
                x: 0, y: 20
            }
        },
        style: {
            widget: false,
            def: false,
            classes: 'qtip-game-style'
        }
    });

    if (obj.hasClass('unparsed-game-tooltip')) {
        obj.removeClass('unparsed-game-tooltip');
    }
}

function getGameTooltipContent(obj) {
    var gameId = obj.attr('gameId');

    var gameTooltip = $('#game-details-tooltip-' + gameId);
    if (gameTooltip) {
        var html = gameTooltip.html();
        return html;
    } else {
        return '<div>Error getting game details</div>';
    }
}

function saveEmail() {
    if (emailSaved)
        return;

    var emailInput = $('#email-address');
    var emailRegEx = /^([a-zA-Z0-9+_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,63}|[0-9]{1,3})(\]?)$/;
    emailInput.removeClass('invalid-input');
    var email = emailInput.val();
    if (emailRegEx.test(email)) {
        $('#delete').hide(500);
        var save = $('#save');
        save.removeClass('save-icon');
        save.addClass('async-icon');
        $.post('/User/UpdateEmail?email=' + encodeURIComponent(email), null,
        function () {
            var saveElement = $('#save');
            saveElement.removeClass('async-icon');
            if (!saveElement.hasClass('check-icon'))
                saveElement.addClass('check-icon');
            emailSaved = true;
        },
        'html');

    } else {
        emailInput.addClass('invalid-input');
    }
}

function deleteEmail() {
    if (emailDeleted)
        return;
    $('#save').hide(500);
    var del = $('#delete');
    del.removeClass('delete-icon');
    del.addClass('async-icon');
    $.post('/User/UpdateEmail', null,
    function () {
        var deleteElement = $('#delete');
        deleteElement.removeClass('async-icon');
        if (!deleteElement.hasClass('check-icon'))
            deleteElement.addClass('check-icon');
        $('#email-address').val("");
        $('#email-address').attr('disabled', true);
        emailDeleted = true;
    },
    'html');

}

function updatePreference(sender) {
    $.post('/User/UpdatePreference?key=' + sender.id + '&value=' + sender.checked, null, function () { }, 'html');
}


function setVacationMode(value) {
    if (value) {
        lockCP = true;
        $("#dialog-confirm-vacation-mode").css({ visibility: "visible" });
        $("#dialog-confirm-vacation-mode").dialog({
            resizable: false,
            height: 180,
            modal: true,
            close: function () {
                setTimeout(function (event, ui) { lockCP = false; }, 500);
            },
            show: "clip",
            hide: "clip",
            buttons: {
                "Yes": function () {
                    $(this).dialog("close");
                    $('#vacation-mode-on').addClass('selected-vacation-box');
                    $('#vacation-mode-off').removeClass('selected-vacation-box');
                    $.post('/User/UpdatePreference?key=VacationMode&value=' + value, null, function () { }, 'html');
                    setTimeout(function () { lockCP = false; }, 500);
                },
                "No": function () {
                    $(this).dialog("close");
                    setTimeout(function () { lockCP = false; }, 500);
                }
            }
        });
    } else {
        $('#vacation-mode-off').addClass('selected-vacation-box');
        $('#vacation-mode-on').removeClass('selected-vacation-box');
        $.post('/User/UpdatePreference?key=VacationMode&value=' + value, null, function () { }, 'html');
    }
}

function setSaveType(value) {
    $.post('/User/UpdatePreference?key=SaveFileType&value=' + value, null, function () { }, 'html');
    $('#hotseat-saves').removeClass('selected-file-type-box');
    $('#single-player-saves').removeClass('selected-file-type-box');
    if (value == 'HotSeat') {
        $('#hotseat-saves').addClass('selected-file-type-box');
    } else {
        $('#single-player-saves').addClass('selected-file-type-box');
    }
}

function setTimeZone(select) {
    var value = select.options[select.selectedIndex].value;
    $.post('/User/UpdatePreference?key=TimeZone&value=' + value, null, function () { }, 'html');
}

function savePassword() {
    var passwordInput = $('#game-password');
    passwordInput.removeClass('invalid-input');
    var password = passwordInput.val();
    if (password.length < 50) {
        $('#password-done').removeClass('check-icon');
        var save = $('#password-save');
        save.removeClass('save-icon');
        save.addClass('async-icon');
        $.post('/User/UpdatePreference?key=GamePassword&value=' + passwordInput.val(),
        null,
        function () {
            var save = $('#password-save');
            save.removeClass('async-icon');
            save.addClass('save-icon');
            var done = $('#password-done');
            if (!done.hasClass('check-icon'))
                done.addClass('check-icon');
        },
        'html');

    } else {
        passwordInput.addClass('invalid-input');
    }
}

function checkUncheckAll(value) {
    var checkboxes = $('#checkboxes').children('input').map(function () {
        var checkbox = $(this);
        if (!checkbox.attr('checked') && value) {
            checkbox.click();
        } else if (checkbox.attr('checked') && !value) {
            checkbox.click();
        }
    });

}

function downloadClicked() {
    window.location.href = document.getElementById("download_link").href;
}

function InitializeDownload() {
    if (HasRuntimeVersion(runtimeVersion, false) || (checkClient && HasRuntimeVersion(runtimeVersion, checkClient))) {
        document.getElementById("download_link").href = directLink;
    }
}

function HasRuntimeVersion(v, c) {
    var va = GetVersion(v);
    var i;
    var a = navigator.userAgent.match(/\.NET CLR [0-9.]+/g);
    if (va[0] == 4)
        a = navigator.userAgent.match(/\.NET[0-9.]+E/g);
    if (c) {
        a = navigator.userAgent.match(/\.NET Client [0-9.]+/g);
        if (va[0] == 4)
            a = navigator.userAgent.match(/\.NET[0-9.]+C/g);
    }
    if (a != null)
        for (i = 0; i < a.length; ++i)
            if (CompareVersions(va, GetVersion(a[i])) <= 0)
                return true;
    return false;
}
function GetVersion(v) {
    var a = v.match(/([0-9]+)\.([0-9]+)\.([0-9]+)/i);
    if (a == null)
        a = v.match(/([0-9]+)\.([0-9]+)/i);
    return a.slice(1);
}
function CompareVersions(v1, v2) {
    if (v1.length > v2.length) {
        v2[v2.length] = 0;
    }
    else if (v1.length < v2.length) {
        v1[v1.length] = 0;
    }

    for (i = 0; i < v1.length; ++i) {
        var n1 = new Number(v1[i]);
        var n2 = new Number(v2[i]);
        if (n1 < n2)
            return -1;
        if (n1 > n2)
            return 1;
    }
    return 0;
}

function loadMorePublicGames() {
    var page = 0;
    var $win = $(window);
    var loading = false;

    $win.scroll(function () {
        loadMore();
    });

    $(document).ready(function () {
        loadMore();
        setLoadingDots();
    });

    function loadMore() {
        if (loading) {
            setInterval(loadMore(), 1000);
        }
        else if ($win.height() + $win.scrollTop() + 180 >= $(document).height() && $("#loading").is(':visible') && morePublicGamesToLoad()) {
            loading = true;
            $.post('/Game/BrowserPage?page=' + ++page, null, function (data) {
                $('#loading').hideLoadingDots();
                $("#loading").remove();
                loading = false;

                $("#game-browser").append(data);
                setLoadingDots();
                initializeGameElements();

                loadMore();
            }, 'html');
        }
    }
}

function morePublicGamesToLoad() {
    return !($("#public-games-end").length > 0);
}

function loadMoreMessages() {
    var page = 0;
    var $win = $(window);
    var loading = false;

    $win.scroll(function () {
        loadMore();
    });

    $(document).ready(function () {
        loadMore();
        setLoadingDots();
    });

    function loadMore() {
        if (loading) {
            setInterval(loadMore(), 1000);
        }
        else if ($win.height() + $win.scrollTop() + 180 >= $(document).height() && $("#loading").is(':visible') && moreMessagesToLoad()) {
            loading = true;
            $.post('/User/MyMessagesPage?page=' + ++page, null, function (data) {
                $('#loading').hideLoadingDots();
                $("#loading").remove();
                loading = false;

                $("#message-list").append(data);
                setLoadingDots();
                initializeGeneralElements();

                loadMore();
            }, 'html');
        }
    }
}

function moreMessagesToLoad() {
    return !($("#my-messages-end").length > 0);
}

function createMessageDialog() {
    loadPlayerRecipient();

    $("#dialog-create-message").show();
    $("#dialog-create-message").dialog({
        resizable: false,
        height: 500,
        modal: true,
        show: "clip",
        hide: "clip",
        buttons: {
            "Create": function () {
                createNewMessage();
            },
            "Cancel": function () {
                clearMessageForm();

                $(this).dialog("close");
            }
        }
    });
}

function loadPlayerRecipient() {
    if (playerToSendMessage) {
        var $new = generateFoundUserItem(playerToSendMessage)
                    .appendTo($("#recipient-list"));
        
        AddTooltip($('img', $new));

        recipients.push(playerToSendMessage.userId);
    }
}

function generateFoundUserItem(player) {
    return $('<li class="found-user"><a href="/Community#' + player.userId + '"><img class="' + player.userId + 'sm avatarsm tooltip draggable" src="' + player.border +
        '" style="background-image:url(' + player.avatar + ')" title="' + player.userName + '" /><input class="user-id" type="hidden" value="' + player.userId + '" /></a></li>');
}

function updateListPreference(id, value, savingFlag) {
    SavingFlags[savingFlag] = true;

    updateRefreshButtonEnabled();

    $.post('/User/UpdatePreference?key=' + id + '&value=' + value,
           null,
           function () {
               SavingFlags[savingFlag] = false;
               updateRefreshButtonEnabled();
           },
           'html');
}

function updateRefreshButtonEnabled() {
    var refreshButton = $("#refresh-public-games");

    if (SavingFlags.IsSavingPreferences()) {
        refreshButton.addClass("disabled-right-box-button");
        refreshButton.addClass("no-select");
    }
    else {
        refreshButton.removeClass("disabled-right-box-button");
        refreshButton.removeClass("no-select");
    }
}

function refreshPublicGames() {
    if (!SavingFlags.IsSavingPreferences()) {
        reloadPublicGames();
    }
}

function setLoadingDots() {
    var loadingDiv = $('#loading');

    if (loadingDiv) {
        loadingDiv.loadingDots({ destination: '#loading-animation' });
        loadingDiv.showLoadingDots();
    }
}

function setCommentLoadingDots() {
    var loadingDiv = $('#loading-comments');

    if (loadingDiv) {
        loadingDiv.loadingDots({ destination: '#loading-comments-animation' });
        loadingDiv.showLoadingDots();
    }
}

function setGameType(type) {
    if ($('#game-type').val() != type) {
        $('#game-type').val(type);
        $('.selector-box').removeClass('selected-selector-box');
        $('#select-type-' + type).addClass('selected-selector-box');

        $('.mod-instructions').slideUp();
        $('#' + type + '-instructions').slideDown();
    }
}

function fileUploadChanged() {
    var fileName = $("#saveFileUpload").val().replace(/^.*[\\\/]/, '');
    $("#fileText").text(fileName);
}

function resizeInviteLinkBox() {
    var linkBox = $('#invite-link')[0];
    var maxrows = 10;

    var lh = linkBox.clientHeight / linkBox.rows;

    while (linkBox.scrollHeight > linkBox.clientHeight && !window.opera && linkBox.rows < maxrows) {
        linkBox.style.overflow = 'hidden';
        linkBox.rows += 1;
    }

    if (linkBox.scrollHeight > linkBox.clientHeight) linkBox.style.overflow = 'auto';
}

function clearPrompt(input) {
    var element = $(input);
    if (isPromptText(element)) {
        element.val("");
        element.removeClass('prompt');
    }
}

function isPromptText(element) {
    return element.val() == element.attr('tag');
}

function postComment(input) {
    $(input).text("Posting...");

    $.ajax(
        {
            type: 'POST',
            url: '/Comment/AddComment',
            data: $("#postNewComment").serialize(),

            success: function (response) {
                if (response) {
                    $('#new-comment-error').text(response).show();
                    $(input).text("Post");
                } else {
                    location.reload();
                }
            }
        });
}

function postCommentEdit(input, commentId) {
    $(input).text("Saving...");

    $.ajax(
        {
            type: 'POST',
            url: '/Comment/EditComment',
            data: $("#postEditComment-" + commentId).serialize(),

            success: function (response) {
                if (response) {
                    $('#edit-comment-error-' + commentId).text(response).show();
                    $(input).text("Save");
                } else {
                    location.reload();
                }
            }
        });
}

function saveDescription(input, gameId) {
    var newDescription = $("#edit-game-description").val();
    if (isGameDescriptionValid(newDescription)) {
        $(input).text("Saving...");

        $.post("/Game/UpdateDescription",
               { id: gameId.toString(), description: newDescription.toString() },
               function (result) {
                   if (result) {
                       $("#save-description-error").text(result);
                       $(input).text("Save Description");
                   } else {
                       loadGameDetails(gameId);
                   }
               }
        );
    } else {
        $("#save-description-error").text("You must enter at least 20 characters for a description, and no more than 2,048")
    }
}

function pad(number, length) {
    var str = '' + number;
    while (str.length < length) {
        str = '0' + str;
    }

    return str;
}

function getFormattedTime(date) {
    var a_p = "";

    var curr_hour = date.getHours();

    if (curr_hour < 12) {
        a_p = "AM";
    }
    else {
        a_p = "PM";
    }
    if (curr_hour == 0) {
        curr_hour = 12;
    }
    if (curr_hour > 12) {
        curr_hour = curr_hour - 12;
    }

    var curr_min = date.getMinutes();

    return curr_hour + ":" + pad(curr_min, 2) + " " + a_p;
}

function loadDates() {
    $('.needs-parsing').each(function (i, obj) {
        var date = new Date(obj.title + " UTC");

        var d = date.getDate();
        var m = date.getMonth() + 1;
        var y = date.getFullYear();
        var time = getFormattedTime(date);
        var existingText = "";

        if (document.all) {
            existingText = obj.innerText;
        } else {
            existingText = obj.textContent;
        }

        var fullString = existingText + " " + m + "/" + d + "/" + y + "  " + time;

        if (document.all) {
            obj.innerText = fullString;
        } else {
            obj.textContent = fullString;
        }

        $(obj).removeClass('needs-parsing');
    });
}

function editComment(commentId) {
    $('#cb-' + commentId).addClass('hide-comment');
    $('#c-' + commentId).removeClass('hide-comment');
}

function confirmStart(gameId) {
    $("#dialog-confirm-start").css({ visibility: "visible" });
    $("#dialog-confirm-start").dialog({
        resizable: false,
        height: 180,
        modal: true,
        show: "clip",
        hide: "clip",
        buttons: {
            "Yes": function () {
                var me = $(this);
                me.dialog("option", "buttons", [{ text: "Starting...", click: function () { } }]);
                
                $.post('/Game/StartGame?id=' + gameId,
                       function (result) {
                           me.dialog("close");
                           if (result) {
                               $('#game-details-error').text(result);
                           } else {
                               loadGameDetails(gameId);
                           }
                       });
            },
            "No": function () {
                $(this).dialog("close");
            }
        }
    });
}

function confirmCommentDelete(commentId) {
    $("#dialog-confirm-comment-delete").show();
    $("#dialog-confirm-comment-delete").dialog({
        resizable: false,
        height: 180,
        modal: true,
        buttons: {
            "Yes": function () {
                var me = $(this);
                me.dialog("option", "buttons", [{ text: "Deleting...", click: function () { } }]);
                
                $.post('/Comment/DeleteComment', { id: commentId },
                function () {
                    me.dialog("close");
                    location.reload();
                });
            },
            "No": function () {
                $(this).dialog("close");
            }
        }
    });
}

function confirmTurnRevert(gameId) {
    $("#dialog-confirm-revert").css({ visibility: "visible" });
    $("#dialog-confirm-revert").dialog({
        resizable: false,
        height: 180,
        modal: true,
        show: "clip",
        hide: "clip",
        buttons: {
            "Yes": function () {
                var me = $(this);
                me.dialog("option", "buttons", [{ text: "Reverting...", click: function() {} }]);

                $.post('/Game/RevertTurn?id=' + gameId,
                        function () {
                            me.dialog("close");
                            loadGameDetails(gameId);
                        });
            },
            "No": function () {
                $(this).dialog("close");
            }
        }
    });
}

function confirmTurnSkip(gameId) {
    $("#dialog-confirm-skip").css({ visibility: "visible" });
    $("#dialog-confirm-skip").dialog({
        resizable: false,
        height: 180,
        modal: true,
        show: "clip",
        hide: "clip",
        buttons: {
            "Yes": function () {
                var me = $(this);
                me.dialog("option", "buttons", [{ text: "Skipping...", click: function () { } }]);

                $.post('/Game/SkipTurn?id=' + gameId,
                        function () {
                            me.dialog("close");
                            loadGameDetails(gameId);
                        });
            },
            "No": function () {
                $(this).dialog("close");
            }
        }
    });
}

function confirmSurrender(gameId) {
    $("#dialog-confirm-surrender").css({ visibility: "visible" });
    $("#dialog-confirm-surrender").dialog({
        resizable: false,
        height: 180,
        modal: true,
        show: "clip",
        hide: "clip",
        buttons: {
            "Yes": function () {
                var me = $(this);
                me.dialog("option", "buttons", [{ text: "Surrendering...", click: function () { } }]);

                $.post('/Game/SurrenderGame?id=' + gameId,
                        function () {
                            me.dialog("close");
                            closeGameDetails();
                        });
            },
            "No": function () {
                $(this).dialog("close");
            }
        }
    });
}

function confirmLeave(gameId) {
    $("#dialog-confirm-leave").css({ visibility: "visible" });
    $("#dialog-confirm-leave").dialog({
        resizable: false,
        height: 180,
        modal: true,
        show: "clip",
        hide: "clip",
        buttons: {
            "Yes": function () {
                var me = $(this);
                me.dialog("option", "buttons", [{ text: "Leaving...", click: function () { } }]);

                $.post('/Game/LeaveGame?id=' + gameId,
                function () {
                    me.dialog("close");
                    closeGameDetails();
                });
            },
            "No": function () {
                $(this).dialog("close");
            }
        }
    });
}

function gameShowInPublic(element, gameId) {
    var showPublic = false;
    var showToggle = $(element).attr('isToggled');

    showPublic = (showToggle == "True") ? false : true;
    $(element).attr('isToggled', (showToggle == "True") ? "False" : "True");

    if (showPublic) {
        $(element).addClass('enabled-dlc-allowed-box');
        $(element).text("Shown in Public");
    } else {
        $(element).removeClass('enabled-dlc-allowed-box');
        $(element).text("Hidden from Public");
    }

    $.post('/Game/UpdateShowInPublic?id=' + gameId + '&showPublic=' + showPublic,
           function () { });
}

function gameDontEncryptPassword(element, gamePlayerId) {
    var encryptPassword = false;
    var showToggle = $(element).attr('isToggled');

    encryptPassword = (showToggle == "True") ? false : true;
    $(element).attr('isToggled', (showToggle == "True") ? "False" : "True");

    if (encryptPassword) {
        $(element).addClass('enabled-dlc-allowed-box');
        $(element).text("Password Encrypted");
    } else {
        $(element).removeClass('enabled-dlc-allowed-box');
        $(element).text("Password Not Encrypted");
    }

    $.post('/Game/UpdateEncryptPassword?id=' + gamePlayerId + '&encryptPassword=' + encryptPassword,
           function () { });
}

function gameAllowVacationMode(element, gamePlayerId) {
    var allowVacation = false;
    var showToggle = $(element).attr('isToggled');

    allowVacation = (showToggle == "True") ? false : true;
    $(element).attr('isToggled', (showToggle == "True") ? "False" : "True");

    if (allowVacation) {
        $(element).addClass('enabled-dlc-allowed-box');
        $(element).text("My Vacation Allowed");
    } else {
        $(element).removeClass('enabled-dlc-allowed-box');
        $(element).text("My Vacation Not Allowed");
    }

    $.post('/Game/UpdateAllowVacation?id=' + gamePlayerId + '&allowVacation=' + allowVacation,
           function () { });
}

function gameAllowPublicJoin(element, gameId) {
    var allowJoin = false;
    var joinToggle = $(element).attr('isToggled');

    allowJoin = (joinToggle == "True") ? false : true;
    $(element).attr('isToggled', (joinToggle == "True") ? "False" : "True");

    if (allowJoin) {
        $(element).addClass('enabled-dlc-allowed-box');
        $(element).text("Public Can Join");
    } else {
        $(element).removeClass('enabled-dlc-allowed-box');
        $(element).text("Invite Only");
    }

    $.post('/Game/UpdateAllowPublicJoin?id=' + gameId + '&allowJoin=' + allowJoin,
           function () { });
}

function gameDlcAllowed(element, gameId) {
    var dlcAllowed = false;
    var dlcToggle = $(element).attr('dlcToggle');

    dlcAllowed = (dlcToggle == "True") ? false : true;
    $(element).attr('dlcToggle', (dlcToggle == "True") ? "False" : "True");

    if (dlcAllowed) {
        $(element).addClass('enabled-dlc-allowed-box');
        $(element).text("DLC Enabled");
    } else {
        $(element).removeClass('enabled-dlc-allowed-box');
        $(element).text("DLC Disabled");
    }

    $.post('/Game/UpdateDlcAllowed?id=' + gameId + '&dlcAllowed=' + dlcAllowed,
               function () { });
}

function confirmCancel(gameId) {
    $("#dialog-confirm-cancel").css({ visibility: "visible" });
    $("#dialog-confirm-cancel").dialog({
        resizable: false,
        height: 180,
        modal: true,
        show: "clip",
        hide: "clip",
        buttons: {
            "Yes": function () {
                var me = $(this);
                me.dialog("option", "buttons", [{ text: "Cancelling...", click: function () { } }]);

                $.post('/Game/CancelGame?id=' + gameId,
                function () {
                    me.dialog("close");
                    closeGameDetails();
                });
            },
            "No": function () {
                $(this).dialog("close");
            }
        }
    });
}

function gamePrivate(element, private, gameId) {
    $('.selector-box').each(function () { $(this).removeClass('selected-selector-box') });
    $(element).addClass('selected-selector-box');
    $.post('/Game/UpdateVisibility?id=' + gameId + '&gamePrivate='
            + private.toString(),
            function () {

            });
}

function updateMaxPlayers(element, players, gameId) {
    if (!$(element).hasClass("disabled-max-player-box")) {
        $('.max-player-box').each(function () {
            $(this).removeClass('selected-max-player-box')
            $(this).removeClass('sub-max-player-box')
        });
        $(element).addClass('selected-max-player-box');
        var foundYet = false;
        $('.max-player-box').each(function () {
            var box = $(this);
            if (box.hasClass('selected-max-player-box')) {
                foundYet = true;
            }
            if (!foundYet && !box.hasClass('disabled-max-player-box')) {
                box.addClass('sub-max-player-box');
            }
        });
        $.post('/Game/UpdateMaxPlayers?id=' + gameId + '&players='
                + players.toString(),
                function () { });
    }
}

function generateInviteLink(gameId) {
    var generateInvite = $('#game-details-content #generate-invite-link');
    generateInvite.removeClass('message-icon');
    generateInvite.addClass('async-icon');
    $.post('/Game/GenerateInviteLink?id=' + gameId,
            function (data) {
                $('#game-details-content #invite-link').val(data);
                resizeInviteLinkBox();

                generateInvite.switchClass('async-icon', 'check-icon', 500);
                window.setTimeout(
                    function () {
                        generateInvite.switchClass('check-icon', 'message-icon', 500);
                    },
                    1500
                );
            }
        );
}

function emailInvite(gameId) {
    var emailAddress = $('#game-details-content #email-invite').val();
    var email = $('#game-details-content #send-email');
    email.removeClass('email-icon');
    email.addClass('async-icon');

    $.post('/Game/SendInviteEmail?id=' + gameId + '&email=' + emailAddress,
               function () {
                   var inviteEmail = $('#game-details-content #email-invite');
                   inviteEmail.val("");

                   var email = $('#game-details-content #send-email');
                   email.switchClass('async-icon', 'check-icon', 500);
                   window.setTimeout(
                       function () {
                           $('#game-details-content #send-email').switchClass('check-icon', 'email-icon', 500);
                       },
                       1500
                   );
               }
        );
}

function initializeGameElements() {
    initializeGeneralElements();
    AddGameTooltip($('.unparsed-game-tooltip'));
}

function initializeGeneralElements() {
    loadDates();
    AddTooltip($('.unparsed-tooltip'));
    AddTooltip($('.tooltip'));
}

function loadMyGames() {
    $.post('/Game/MyGames', null, function (data) {
        $("#my-games").html(data);
        $('#loading-my-games').hideLoadingDots();
        $("#loading-my-games").remove();

        initializeGameElements();
    }, 'html');
}

function loadPublicGames() {
    $.post('/Game/PublicGames', null, function (data) {
        $("#public-games-content").html(data);
        $('#loading-public-games').hideLoadingDots();
        $("#loading-public-games").hide();

        initializeGameElements();
    }, 'html');
}

function reloadPublicGames() {
    $("#public-games-content").empty();
    $("#loading-public-games").show();
    $("#loading-public-games").showLoadingDots();

    loadPublicGames();
}

function loadGameDetails(gameId) {
    $("#game-details-content").empty();

    $("#loading-game-details").show();
    $('#loading-game-details').showLoadingDots();

    $.post('/Game/Details?id=' + gameId, null, function (data) {
        $("#game-details-content").html(data);

        setupGameDetails();

        $('#loading-game-details').hideLoadingDots();
        $("#loading-game-details").hide();

        initializeGeneralElements();
        selectSubTab('#gameDetailBody', '#game-details-sub');
        loadGameDetailsTurns(gameId);
    }, 'html');
}

function setupGameDetails() {
    try {
        initializeGameDetailControls();

        var gameName = $("#game-details-content").find(".game-title-text:first").text().trim();

        setGameDetailName(gameName);
    } catch (err) { }
}

function goToGameDetails(gameId) {
    if (window.location.href.indexOf("/Game") > -1) {
        showGameDetails(gameId);
        return false;
    } else {
        return true;
    }
}

function showGameDetails(gameId) {
    setGameDetailName("Loading...");
    $("#tab-game-details").show();

    selectTab('#tab-game-details', '#game-details');

    document.location.hash = gameId;
    lastGameDetailsId = gameId;

    loadGameDetails(gameId);
}

function setGameDetailName(gameName) {
    $("#link-game-details").text(gameName);
}

function setLocationHash(hash) {
    document.location.hash = hash;
}

function initializeGameIndexPage() {
    var loaded = false;

    selectTab(defaultTab, defaultTabContent);

    $('#loading-public-games').loadingDots({ destination: '#loading-public-games-animation' });
    $('#loading-my-games').loadingDots({ destination: '#loading-my-games-animation' });
    $('#loading-game-details').loadingDots({ destination: '#loading-game-details-animation' });

    $('#loading-my-games').showLoadingDots();
    $('#loading-public-games').showLoadingDots();

    if (document.location.hash) {
        var hashValue = document.location.hash.substring(1);

        if (hashValue.match(/^\d+$/)) {
            showGameDetails(hashValue);
            loaded = true;
        }
        else if (hashValue === "public") {
            selectTab("#tab-public-games", "#public-games");
            loadFirstPublicGames();
            loaded = true;
        }
    }

    if (!loaded) {
        if (isUserAuthenticated) {
            loadFirstMyGames();
        } else {
            loadFirstPublicGames();
        }
    }
}

function createGame() {
    var name = $("#game-name").val();
    var description = $("#game-description").val();
    var steamGameID = $("#SteamGameID").val();
    var type = $('#game-type').val();

    if (validateGameName() && validateGameDescription()) {
        if (type == 'Scenario') {
            $('.ui-dialog-buttonset').css('min-height', '36px');
            $('.ui-dialog-buttonset').css('min-width', '750px');
            $('.ui-dialog-buttonset').children('button').hide(500);
            $('.ui-dialog-buttonset').addClass('dialog-loader');

            $('#scenarioName').val(name);
            $('#scenarioGame').val(steamGameID);
            $('#scenarioDescription').val(description);

            document.forms["createScenario"].submit();
        }
        else {
            $.get("/Game/Create?name=" + escape(name) + "&steamGameID=" + steamGameID + "&type=" + type + "&description=" + escape(description),
                function (data) {
                    if (data.match(/^\d+$/)) {
                        window.location = "/Game/ChangeCiv?id=" + data;
                    } else {
                        $("#game-create-error")
                            .text(data)
                            .show();
                    }
                });
        }
    }
}

function validateGameName() {
    var input = $("#game-name");
    var name = input.val();

    input.removeClass("invalid-input");
    $('#game-name-error').hide();

    if (name.length > 50 || name.length < 1 || input.hasClass("watermark")) {
        input.addClass("invalid-input");
        $('#game-name-error').show();
        input.focus();
        return false;
    }

    return true;
}

function validateGameDescription() {
    var input = $("#game-description");
    clearPrompt(input);
    var description = input.val();

    input.removeClass("invalid-input");
    $('#game-description-error').hide();

    if (!isGameDescriptionValid(description)) {
        input.addClass("invalid-input");
        $('#game-description-error').show();
        input.focus();
        return false;
    }

    return true;
}

function isGameDescriptionValid(description) {
    return description.length <= 2048 && description.length >= 20;
}

function closeGameDetails() {
    $("#game-details-content").empty();

    $("#loading-game-details").show();
    $('#loading-game-details').showLoadingDots();

    selectTab('#tab-public-games', '#public-games');
    loadFirstPublicGames();

    $("#tab-game-details").hide();
}

function loadFirstMyGames() {
    if (!myGamesLoaded) {
        myGamesLoaded = true;

        if (isUserAuthenticated) {
            loadMyGames();
        }
    }
}

function loadFirstPublicGames() {
    if (!publicGamesLoaded) {
        publicGamesLoaded = true;

        loadPublicGames();
    }
}

function loadFirstPlayers() {
    if (!playersLoaded) {
        playersLoaded = true;

        loadPlayers();
    }
}

function loadPlayers() {
    $.post('/Community/Players', null, function (data) {
        $("#players").html(data);
        $('#loading-players').hideLoadingDots();
        $("#loading-players").remove();

        initializeGeneralElements();
    }, 'html');
}

function loadFirstTurns() {
    if (!turnsLoaded) {
        turnsLoaded = true;

        loadTurns();
    }
}

function loadTurns() {
    $.post('/Community/Turns', null, function (data) {
        $("#turns").html(data);
        $('#loading-turns').hideLoadingDots();
        $("#loading-turns").remove();

        initializeGeneralElements();
    }, 'html');
}

function showPlayerDetails(playerId) {
    setPlayerDetailName("Loading...");
    $("#tab-player-details").show();

    selectTab('#tab-player-details', '#player-details');

    document.location.hash = playerId;
    lastPlayerDetailsId = playerId;

    loadPlayerDetails(playerId);
}

function setPlayerDetailName(playerName) {
    $("#link-player-details").text(playerName);
}

function loadPlayerDetails(userId) {
    $("#player-details-content").empty();

    $("#loading-player-details").show();
    $('#loading-player-details').showLoadingDots();

    $.post('/Community/PlayerDetails?id=' + userId, null, function (data) {
        $("#player-details-content").html(data);

        setupPlayerDetails();

        $('#loading-player-details').hideLoadingDots();
        $("#loading-player-details").hide();

        initializeGeneralElements();
    }, 'html');
}

function setupPlayerDetails() {
    try {
        var playerName = $("#player-details-content").find(".player-detail-name").text().trim();

        setPlayerDetailName(playerName);
    } catch (err) { }
}

function initializeCommunityIndexPage() {
    var loaded = false;

    selectTab('#tab-players', '#players');

    $('#loading-players').loadingDots({ destination: '#loading-players-animation' });
    $('#loading-turns').loadingDots({ destination: '#loading-turns-animation' });
    $('#loading-player-details').loadingDots({ destination: '#loading-player-details-animation' });

    $('#loading-players').showLoadingDots();
    $('#loading-turns').showLoadingDots();

    if (document.location.hash) {
        var hashValue = document.location.hash.substring(1);

        if (hashValue.match(/^\d+$/)) {
            showPlayerDetails(hashValue);
            loaded = true;
        }
        else if (hashValue === "forums") {
            selectTab("#tab-forums", "#forums");

            loaded = true;
        }
        else if (hashValue === "turns") {
            selectTab("#tab-turns", "#turns");
            loadFirstTurns();
        }
    }

    if (!loaded) {
        loadFirstPlayers();
    }
}

function loadFirstMessages() {
    if (!messagesLoaded) {
        messagesLoaded = true;

        if (isUserAuthenticated) {
            loadMessages();
        }
    }
}

function loadMessages() {
    $.post('/User/MyMessages', null, function (data) {
        $("#messages").html(data);
        $('#loading-messages').hideLoadingDots()
                              .remove();

        initializeGeneralElements();
    }, 'html');
}

function goToMessageDetails(messageId) {
    if (window.location.href.indexOf("/User/Messages") > -1) {
        showMessageDetails(messageId);
        return false;
    } else {
        return true;
    }
}

function showMessageDetails(messageId) {
    setMessageDetailName("Loading...");
    $("#tab-message-details").show();

    selectTab('#tab-message-details', '#message-details');

    document.location.hash = messageId;
    lastMessageDetailsId = messageId;

    loadMessageDetails(messageId);
}

function setMessageDetailName(messageName) {
    $("#link-message-details").text(messageName);
}

function loadMessageDetails(messageId) {
    $("#message-details-content").empty();

    $("#loading-message-details").show()
                                 .showLoadingDots();

    $.post('/User/MessageDetails?id=' + messageId, null, function (data) {
        $("#message-details-content").html(data);

        setupMessageDetails();

        $('#loading-message-details').hideLoadingDots()
                                     .hide();

        initializeGeneralElements();
    }, 'html');
}

function setupMessageDetails() {
    try {
        var messageName = $("#message-details-content").find(".message-detail-name").text().trim();

        setMessageDetailName(messageName);
    } catch (err) { }
}

function initializeMessageIndexPage() {
    var loaded = false;

    selectTab('#tab-messages', '#messages');

    $('#loading-messages').loadingDots({ destination: '#loading-messages-animation' });
    $('#loading-message-details').loadingDots({ destination: '#loading-message-details-animation' });

    $('#loading-messages').showLoadingDots();

    if (document.location.hash) {
        var hashValue = document.location.hash.substring(1);

        if (hashValue.match(/^\d+$/)) {
            showMessageDetails(hashValue);
            loaded = true;
        }
    }

    if (!loaded) {
        loadFirstMessages();
    }
}

function stripNonNumeric(text) {
    var string = new String(text);
    return string.replace(/[^0-9]/g, '');
}


function nextPlayerPage() {
    if (pagedPlayerList.currentPage < pagedPlayerList.totalPages - 1) {

        pagedPlayerList.currentPage += 1;
        $('#player-page').val(pagedPlayerList.currentPage + 1);


        var currentPage = pagedPlayerList.currentPage;
        setTimeout(function () {
            if (currentPage == pagedPlayerList.currentPage) {
                loadPlayerPage();
            }
        }, playersDelayTime);
    }
}

function previousPlayerPage() {
    if (pagedPlayerList.currentPage > 0) {
        pagedPlayerList.currentPage -= 1;
        $('#player-page').val(pagedPlayerList.currentPage + 1);

        var currentPage = pagedPlayerList.currentPage;
        setTimeout(function () {
            if (currentPage == pagedPlayerList.currentPage) {
                loadPlayerPage();
            }
        }, playersDelayTime);
    }
}

function pageTextChanged() {
    var pageInput = $('#player-page');
    pageInput.val(stripNonNumeric(pageInput.val()));

    var pageNum = pageInput.val();
    if (pageNum == '' || pageNum < 1) {
        pageNum = 1;
    } else if (pageNum > pagedPlayerList.totalPages) {
        pageNum = pagedPlayerList.totalPages;
    }

    pageInput.val(pageNum);
    pagedPlayerList.currentPage = pageNum - 1;

    var currentPage = pagedPlayerList.currentPage;
    setTimeout(function () {
        if (currentPage == pagedPlayerList.currentPage) {
            loadPlayerPage();
        }
    }, playersDelayTime);
}

function loadPlayerPage() {
    setPlayerPageContent('');

   if (isPlayerPageCached(pagedPlayerList.currentPage)) {
       setPlayerPageContent(getPlayerPageFromCache(pagedPlayerList.currentPage));
   } else {
       getPlayerPageFromServer();
   }
}

function getPlayerPageFromServer() {
    var pageNumber = pagedPlayerList.currentPage;
    var columnName = pagedPlayerList.currentColumn;
    var sortAscending = (pagedPlayerList.columnSort[columnName] == 1);

    var queryUrl = pagedPlayerList.queryUrl +
                   "?page=" + pageNumber +
                   "&columnName=" + columnName +
                   "&sortAscending=" + sortAscending +
                   "&findPlayerName=" + pagedPlayerList.findPlayerName;
    
    showHidePlayerPageLoading(true);

    
    $.get(queryUrl,
           null,
           function (data) {
               showHidePlayerPageLoading(false);

               if (pageNumber == pagedPlayerList.currentPage) {
                   setPlayerPageContent(data);
                   savePlayerPageToCache(pageNumber, data);
                   getTotalPageCountFromResponse(data);
               }
           },
           'html');
}

function setPlayerPageContent(data) {
    var pageContent = $("#player-list-content");
    pageContent.html(data);

    setTimeout(function () {
        AddTooltip($('.tooltip'));
    }, 500);
}

function getTotalPageCountFromResponse(data) {
    var pagesAvailable = $(data).filter('#total-pages-available');
    var pageCount = parseInt(pagesAvailable.val());

    pagedPlayerList.totalPages = pageCount;
    $('.page-of').html("of <strong>" + pageCount + "</strong>");

}

function showHidePlayerPageLoading(show) {
    var loadingElement = $('#player-list-loading');

    if (show) {
        loadingElement.show()
                      .showLoadingDots();
    } else {
        loadingElement.hideLoadingDots()
                      .hide();
    }
}

function initializePlayerPageLoadingDots() {
    var loadingId = '#player-list-loading';

    $(loadingId).loadingDots({ destination: loadingId });
}

function findUserPage() {
    if (pagedPlayerList.userPage == -1) {
        getUserPage();
    } else {
        navigateToUserPage();
    }
}

function getUserPage() {
    var columnName = pagedPlayerList.currentColumn;
    var sortAscending = (pagedPlayerList.columnSort[columnName] == 1);
    var queryUrl = pagedPlayerList.userPageQueryUrl +
                   "?columnName=" + columnName +
                   "&sortAscending=" + sortAscending +
                   "&findPlayerName=" + pagedPlayerList.findPlayerName;

    $.get(queryUrl,
        null,
        function (data) {
            var pageNum = parseInt(stripNonNumeric(data));
            if (pageNum >= 0 && pageNum < pagedPlayerList.totalPages - 1) {

                pagedPlayerList.userPage = pageNum;
                navigateToUserPage();
            }
        },
        'html');
}

function navigateToUserPage() {
    pagedPlayerList.currentPage = pagedPlayerList.userPage;

    $('#player-page').val(pagedPlayerList.currentPage + 1);

    loadPlayerPage();
}

function isPlayerPageCached(pageNum) {
    return pagedPlayerList.pageCache[pageNum] != undefined
        && pagedPlayerList.pageCache[pageNum].length > 0;
}

function getPlayerPageFromCache(pageNum) {
    return pagedPlayerList.pageCache[pageNum];
}

function savePlayerPageToCache(pageNum, pageData) {
    pagedPlayerList.pageCache[pageNum] = pageData;
}

function clearPlayerPageCache() {
    pagedPlayerList.pageCache = {};
    pagedPlayerList.userPage = -1;
}

function setColumnSortType(columnName, sortType) {
    pagedPlayerList.columnSort[columnName] = sortType;

    var columnIcon = getColumnSortIcon(columnName);
    
    columnIcon.removeClass('sort-ascending')
              .removeClass('sort-descending');

    if (sortType == 1) {
        columnIcon.addClass('sort-ascending');
    } else if (sortType == 2) {
        columnIcon.addClass('sort-descending');
    }
}

function getColumnSortIcon(columnName) {
    return $(".player-" + columnName + " .player-sort-icon");
}

function clearAllColumnSorts() {
    for (var i = 0; i < columnNames.length; i++) {
        pagedPlayerList.columnSort[columnNames[i]] = 0;
    }

    $('.player-sort-icon').removeClass('sort-ascending')
                          .removeClass('sort-descending');
}

function toggleColumnSort(columnName) {
    var sortType = pagedPlayerList.columnSort[columnName];

    if (sortType == 2) {
        sortType = 1;
    } else {
        sortType = 2;
    }

    clearAllColumnSorts();
    setColumnSortType(columnName, sortType);

    setTimeout(function () {
        if (pagedPlayerList.columnSort[columnName] == sortType) {
            pagedPlayerList.currentColumn = columnName;
            
            clearPlayerPageCache();

            pagedPlayerList.currentPage = 0;
            $('#player-page').val(pagedPlayerList.currentPage + 1);

            loadPlayerPage();
        }
    }, playersDelayTime);
}

function findPlayer() {
    var playerName = $('#find-player-input').val();

    if (playerName != pagedPlayerList.findPlayerName) {
        pagedPlayerList.findPlayerName = playerName;
        pagedPlayerList.currentPage = 0;

        clearPlayerPageCache();
        

        loadPlayerPage();
    }
}

function checkPlayerFilterKey(e) {
    if (e.keyCode == 13) {
        findPlayer();
        return false;
    }

    return true;
}

function clearPlayerFilter() {
    var playerFilterInput = $('#find-player-input');
    playerFilterInput.val("Filter by player name");
    playerFilterInput.addClass('watermark');

    pagedPlayerList.findPlayerName = '';
    clearPlayerPageCache();

    loadPlayerPage();
}

function loadPlayerDetailsGames(playerId) {
    $('#loading-player-games-content').loadingDots({ destination: '#loading-player-games-content-animation' })
                                      .showLoadingDots();

    var queryUrl = "/Community/PlayerDetailGames?id=" + playerId;

    $.get(queryUrl,
          null,
          function(data) {
              var contentDiv = $('#player-games-' + playerId + '-content');
              
              contentDiv.html(data);

              initializeGameElements();
          },
          'html');
}

function loadPlayerRecentTurns(playerId) {
    $('#loading-player-recent-turns-content').loadingDots({ destination: '#loading-player-recent-turns-content-animation' })
                                             .showLoadingDots();

    var queryUrl = "/Community/PlayerDetailTurns?id=" + playerId;

    $.get(queryUrl,
          null,
          function (data) {
              $('#player-recent-turns-' + playerId + '-content').html(data);
          },
          'html');
}

function loadGameDetailsTurns(gameId) {
    $('#loading-game-recent-turns-content').loadingDots({ destination: '#loading-game-recent-turns-content-animation' })
                                             .showLoadingDots();

    var queryUrl = "/Game/GameDetailTurns?id=" + gameId;

    $.get(queryUrl,
          null,
          function (data) {
              $('#game-recent-turns-' + gameId + '-content').html(data);
          },
          'html');
}

function addRecipient(eventData, ui) {
    var item = $(ui.item);

    var userId = '';
    var hiddenId = $('input.user-id', item);
    if (hiddenId != null) {
        userId = hiddenId.val();
    }

    if (recipients.indexOf(userId) == -1) {
        recipients.push(userId);
    } else {
        item.remove();
    }
}

function removeRecipient(eventData, ui) {
    var item = $(ui.item);

    var userId = '';
    var hiddenId = $('input.user-id', item);
    if (hiddenId != null) {
        userId = hiddenId.val();
    }

    removeA(recipients, userId);

    item.hide(500);
}

function clearMessageForm() {
    recipients = [];

    $('#recipient-list').empty();
    $('#recipients-error').hide();
    $('#message-text-error').hide();
    $('#new-message-error').hide();
    $('#found-list').empty();
    $('#found').hide();
    var messageInput = $('#message-text').addClass('prompt');
    messageInput.val(messageInput.attr('tag'));
}

function createNewMessage() {
    if (isRecipientsValid() && isMessageBodyValid()) {
        submitNewMessage();
    }
}

function submitNewMessage() {
    var queryUrl = "/User/CreateNewMessage" +
                   "?messageBody=" + escape($('#message-text').val()) +
                   "&recipientList=" + recipients.join() +
                   "&appendToExisting=" + $('#message-append-existing').is(':checked');

    $.post(queryUrl,
           null,
           function (response) {
               if (response.length > 0) {
                   $('#new-message-error').text(response)
                                          .show();
               } else {
                   window.location = '/User/Messages';
               }
           },
           'text');
}

function isRecipientsValid() {
    var errorMessage = $('#recipients-error');

    if (recipients.length > 0) {
        errorMessage.hide();
        return true;
    } else {
        errorMessage.show();
        return false;
    }
}

function isMessageBodyValid() {
    var errorMessage = $('#message-text-error');
    var messageInput = $('#message-text');
    clearPrompt(messageInput);
    var messageLength = messageInput.val().length;

    if (messageLength > 2 && messageLength < 2048) {
        errorMessage.hide();
        return true;
    } else {
        errorMessage.show();
        return false;
    }
}

function markMessageAsRead(messageId, link) {
    var queryUrl = "/User/MarkMessageAsRead?id=" + messageId;

    $.post(queryUrl,
           null,
           function(response) {
               $(link).remove();
               $('#message-' + messageId).removeClass('message-has-new');
           },
           'text');
}

function initializeCommentsBase(id, type) {
    var page = 0;
    var $win = $(window);
    var loading = false;

    $win.scroll(function () {
        loadMore();
    });

    $(document).ready(function () {
        loadMore();
        setCommentLoadingDots();
    });

    function loadMore() {
        if (loading) {
            setInterval(loadMore(), 1000);
        }
        else if ($win.height() + $win.scrollTop() + 180 >= $(document).height() && $("#loading-comments").is(':visible')) {
            loading = true;
            $.post('/Comment/CommentPage?id=' + id + '&type=' + type + '&page=' + ++page, null, function (data) {
                $("#loading-comments").hideLoadingDots();
                $("#loading-comments").remove();
                loading = false;

                $("#comment-list-" + id).append(data);

                AddTooltip($('.unparsed-tooltip'));

                loadDates();
                setCommentLoadingDots();
                loadMore();
            }, 'html');
        }
    }
}
