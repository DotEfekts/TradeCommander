function bindToInput(inputEl) {
    inputEl.addEventListener("keydown", function (e) {
        if (e.key === "Tab")
            e.preventDefault();
    });

    document.addEventListener("mouseup", function (e) {
        var highlightedText = "";
        if (window.getSelection) {
            highlightedText = window.getSelection().toString();
        }
        else if (document.selection && document.selection.type !== "Control") {
            highlightedText = document.selection.createRange().text;
        }

        if (!(e.srcElement instanceof HTMLInputElement) && (!highlightedText || highlightedText === ""))
            inputEl.focus();
    })
}

function scrollConsoleDown(consoleEl) {
    window.setTimeout(function() {
        consoleEl.scrollTop = consoleEl.scrollHeight;
    }, 0);
}

function moveCaretToEnd(inputEl) {
    window.setTimeout(function () {
        inputEl.selectionStart = inputEl.selectionEnd = inputEl.value.length;
    }, 0);
}

function setCssVar(name, value) {
    if(!value || isValidColor(value))
        document.documentElement.style.setProperty(name, value);
    else
        document.documentElement.style.setProperty(name, null);

    return !value || isValidColor(value);
}

function setBodyClass(className) {
    document.body.className = className;
}

function isValidColor(strColor) {
    var s = new Option().style;
    s.color = strColor;
    return s.color.startsWith("rgb(") || s.color.startsWith("rgba(") || s.color === strColor.toLowerCase();
}

function renderMap(locations, width, height, shipData, shipFocus, locationSymbol, flightPlans) {
    const scale = 5;
    let zoom = 1;

    const canvas = document.getElementById('map');
    canvas.width = width * scale;
    canvas.height = height * scale;

    if (canvas.getContext) {
        locations.sort(sortLocation);

        const ctx = canvas.getContext('2d');

        let xTranslate = canvas.width / 2;
        let yTranslate = canvas.height / 2;

        if (shipFocus) {
            if (shipFocus.lastFlightPlan && shipFocus.flightEnded === false) {
                let flightPlanStart = locations.find(l => l.symbol === shipFocus.lastFlightPlan.departure);
                let flightPlanEnd = locations.find(l => l.symbol === shipFocus.lastFlightPlan.destination);

                if (flightPlanStart && flightPlanEnd) {
                    zoom = 2.5;

                    let x1 = flightPlanStart.x;
                    let y1 = -flightPlanStart.y;
                    let x2 = flightPlanEnd.x;
                    let y2 = -flightPlanEnd.y;
                    let shipProgress = shipFocus.timeElapsed / (shipFocus.timeElapsed + shipFocus.lastFlightPlan.timeRemainingInSeconds);

                    let shipX = x1 + ((x2 - x1) * shipProgress);
                    let shipY = y1 + ((y2 - y1) * shipProgress);

                    xTranslate = xTranslate - (shipX * scale * zoom);
                    yTranslate = yTranslate - (shipY * scale * zoom);
                }
            } else if (locationSymbol) {
                let location = locations.find(l => l.symbol === locationSymbol);
                let x1 = location.x;
                let y1 = -location.y;

                xTranslate = xTranslate - (x1 * scale * zoom);
                yTranslate = yTranslate - (y1 * scale * zoom);
            }
        }

        var colour = getComputedStyle(document.documentElement).getPropertyValue('--content-color');
        ctx.strokeStyle = colour
        ctx.fillStyle = colour;
        ctx.lineWidth = 1;

        let fontSize = 18 * (zoom === 1 ? 1 : 1.5)
        ctx.font = fontSize + "px 'Share Tech Mono'";

        ctx.beginPath();
        ctx.moveTo(0, yTranslate);
        ctx.lineTo(canvas.width, yTranslate);
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(xTranslate, 0);
        ctx.lineTo(xTranslate, canvas.height);
        ctx.stroke();

        for (let i = 0; i < locations.length; i++) {
            let location = locations[i];

            let isLargeBody = location.type === "PLANET" || location.type === "GAS_GIANT";
            let x = location.x;
            let y = -location.y;


            ctx.beginPath();
            ctx.arc((x * scale * zoom) + xTranslate, (y * scale * zoom) + yTranslate, (isLargeBody ? 1.5 : 0.75) * scale * zoom, 0, 2 * Math.PI, false);
            if (isLargeBody)
                ctx.stroke();
            else
                ctx.fill();
        }

        let textLocations = [];
        for (let i = 0; i < locations.length; i++) {
            let location = locations[i];

            let isLargeBody = location.type === "PLANET" || location.type === "GAS_GIANT";
            let x = ((location.x + (isLargeBody ? 2.5 : 2)) * scale * zoom) + xTranslate;
            let y = (-location.y * scale * zoom) + yTranslate;

            let textSize = ctx.measureText(location.symbol);
            let textLoc = { x1: x - 1, y1: y - 1, x2: x + textSize.width + 1, y2: y + fontSize + 1 };

            for (let i = 0; i < textLocations.length; i++) {
                let testLoc = textLocations[i];

                if (overlaps(textLoc, testLoc))
                    if (textLoc.y1 - testLoc.y1 > 0) {
                        textLoc.y1 = testLoc.y2;
                        textLoc.y2 = testLoc.y2 + fontSize + 1;
                    } else {
                        textLoc.y1 = testLoc.y1 - (fontSize + 1);
                        textLoc.y2 = testLoc.y1;
                    }
            }

            ctx.fillText(location.symbol, textLoc.x1 + 1, textLoc.y1 + 1);
            textLocations.push(textLoc);
        }

        for (let i = 0; i < shipData.length; i++) {
            let ship = shipData[i];
            if (ship.lastFlightPlan && ship.flightEnded === false) {
                let flightPlanStart = locations.find(l => l.symbol === ship.lastFlightPlan.departure);
                let flightPlanEnd = locations.find(l => l.symbol === ship.lastFlightPlan.destination);
                
                if (flightPlanStart && flightPlanEnd) {
                    let x1 = flightPlanStart.x;
                    let y1 = -flightPlanStart.y;
                    let x2 = flightPlanEnd.x;
                    let y2 = -flightPlanEnd.y;

                    ctx.beginPath();
                    ctx.moveTo((x1 * scale * zoom) + xTranslate, (y1 * scale * zoom) + yTranslate);
                    ctx.lineTo((x2 * scale * zoom) + xTranslate, (y2 * scale * zoom) + yTranslate);
                    ctx.stroke();

                    let shipProgress = ship.timeElapsed / (ship.timeElapsed + ship.lastFlightPlan.timeRemainingInSeconds);
                    let shipX = x1 + ((x2 - x1) * shipProgress);
                    let shipY = y1 + ((y2 - y1) * shipProgress);

                    ship.x = shipX;
                    ship.y = shipY;

                    ctx.beginPath();
                    ctx.fillRect(((shipX - 0.5) * scale * zoom) + xTranslate, ((shipY - 0.5) * scale * zoom) + yTranslate, 1 * scale * zoom, 1 * scale * zoom);
                }
            }
        }

        shipData.sort(sortLocation);

        for (let i = 0; i < shipData.length; i++) {
            let ship = shipData[i];

            if (ship.x && ship.y) {
                let textSize = ctx.measureText(ship.displayName);

                let x = ((ship.x - 0.5) * scale * zoom) + (xTranslate - textSize.width);
                let y = ((ship.y + 3) * scale * zoom) + yTranslate;

                let textLoc = { x1: x, y1: y - 1, x2: x + textSize.width, y2: y + fontSize + 1 };

                for (let i = 0; i < textLocations.length; i++) {
                    let testLoc = textLocations[i];

                    if (overlaps(textLoc, testLoc) && textLoc.y1 < testLoc.y2)
                    {
                        textLoc.y1 = testLoc.y2;
                        textLoc.y2 = testLoc.y2  + fontSize + 1;
                    }
                }

                ctx.fillText(ship.displayName, textLoc.x1, textLoc.y1 + 1);
                textLocations.push(textLoc);
            }
        }

        if (flightPlans !== null) {
            for (let i = 0; i < flightPlans.length; i++) {
                let plan = flightPlans[i];
                let flightPlanStart = locations.find(l => l.symbol === plan.departure);
                let flightPlanEnd = locations.find(l => l.symbol === plan.destination);

                if (flightPlanStart && flightPlanEnd) {
                    let x1 = flightPlanStart.x;
                    let y1 = -flightPlanStart.y;
                    let x2 = flightPlanEnd.x;
                    let y2 = -flightPlanEnd.y;

                    let shipProgress = plan.timeElapsed / (plan.timeElapsed + plan.timeRemaining);
                    let shipX = x1 + ((x2 - x1) * shipProgress);
                    let shipY = y1 + ((y2 - y1) * shipProgress);

                    plan.x = shipX;
                    plan.y = shipY;

                    ctx.beginPath();
                    ctx.strokeRect(((shipX - 0.5) * scale * zoom) + xTranslate, ((shipY - 0.5) * scale * zoom) + yTranslate, 1 * scale * zoom, 1 * scale * zoom);
                }
            }
        }
    }

    function overlaps(a, b) {
        if (a.x1 >= b.x2 || b.x1 >= a.x2) return false;
        if (a.y1 >= b.y2 || b.y1 >= a.y2) return false;
        return true;
    }
}

function sortLocation(el1, el2) {
    if (el1.y < el2.y)
        return 1;
    else if (el1.y > el2.y)
        return -1;

    if (el1.x < el2.x)
        return 1;
    else if (el1.x > el2.x)
        return -1;
    else
        return 0;
}