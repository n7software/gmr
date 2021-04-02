var cp;
var panelclosed;
var notpanel;

function setCp(value) {
    cp = value;
}

function setPanelClosed(value) {
    panelclosed = value;
}

function setNotPanel(value) {
    notpanel = value;
}

function notificationClicked(value) {
    $.post(panelclosed, null, function () { window.location = value; }, 'html');
}

$(document).ready(function () {
    $('#notification-num').click(function (e) {
        e.stopPropagation();
        $('#cp-popup').hide(100, function () {
            $('#notification-popup').html('<div class="panel-loader"></div>');
            $('#notification-popup').toggle(100,
            function () {
                if ($('#notification-popup').is(':hidden')) {
                    $.post(panelclosed, null, function () { }, 'html');
                }
                else {
                    $.post(notpanel, null, function (data) {
                        $('#notification-popup').html(data);
                    }, 'html');
                }
            });
        });

    });
});

function ShowCP(e) {
    if (e != null) {
        e.stopPropagation();
    }

    $('#notification-popup').hide(100, function () {
        $('#cp-popup').html('<div class="panel-loader"></div>');
        $('#cp-popup').toggle(100,
        function () {
            $.post(cp, null, function (data) {
                $('#cp-popup').html(data);
            }, 'html');
        });
    });
}

$(document).ready(function() {
    $('#avatar').click(ShowCP);
});