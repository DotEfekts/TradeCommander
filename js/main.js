(function () {
    document.onmouseup = function () {
        var highlightedText = "";
        if (window.getSelection) {
            highlightedText = window.getSelection().toString();
        }
        else if (document.selection && document.selection.type !== "Control") {
            highlightedText = document.selection.createRange().text;
        }

        if (highlightedText === "") {
            var input = document.getElementById('command-input');
            if(input)
            input.focus();
        }
    }
})();