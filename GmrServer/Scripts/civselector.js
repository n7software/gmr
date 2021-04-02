var selectorPopup;
var civSelector;

function setSelectorPopup(value) {
    selectorPopup = value;
}

function setCivSelector(value) {
    civSelector = value;
}

makeUnselectable($('civ-selector').get());

$(document).ready(function () {
    fleXenv.fleXcrollMain("selector-popup-wrapper");
    $('#selector-popup-wrapper').hide();
});


$(document).ready(function () {
    $('#civ-selector').click(function (e) {
        e.stopPropagation();
    });
});

$(document).ready(function () {
    $('#selector-popup-wrapper').click(function (e) {
        e.stopPropagation();
    });
});

function updateSelector(id, sender) {

    var name = sender.firstElementChild.firstElementChild.firstElementChild.innerText;
    var leader = sender.firstElementChild.firstElementChild.lastElementChild.innerText;

    $("#CivID").val(id);
    $("#Name").val(name);
    $("#Leader").val(leader);

    $.post(selectorPopup + id, null, function (data) {
        $('#selector-popup').html(data);
        fleXenv.updateScrollBars();
        $('#selector-popup-wrapper').hide(100);
    }, 'html');

    $.post(civSelector + id, null, function (data) {
        $('#civ-selector').html(data);
    }, 'html');
}

function toggleSelectorPopup() {
    $('#selector-popup-wrapper').toggle(100);
}
