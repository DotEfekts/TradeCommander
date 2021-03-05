function scrollConsoleDown() {
    window.setTimeout(function() {
        var console = document.getElementById('console');
        console.scrollTop = console.scrollHeight;
    }, 0);
}

function moveCaretToEnd() {
    window.setTimeout(function () {
        var input = document.getElementById('command-input');
        input.selectionStart = input.selectionEnd = input.value.length;
    }, 0);
}