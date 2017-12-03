//////////////////////////////////////////////////////////////////
//                          si-data.js                          //
//////////////////////////////////////////////////////////////////

// Read and display the data coming from punches to a station  //

/* ------ Import and Set Up Variables ----- */

const si = require('./si-variables.js');
const crc = require('./crc.js')
const card5 = new si.card5();
const card10 = new si.card10();

/* ------ HTML Elements ----- */

const connect = document.getElementById('connect');


/* ----- Functions -----*/

function output(data, type) {
    // Display the data fromm download in #download-output as <p> with different stylings given by classes

    const downloadOutput = document.getElementById('download-output');

    if (type == 'error') {
        downloadOutput.innerHTML = downloadOutput.innerHTML + "<p class='error'>" + data + "</p>";
    }
    else if (type == 'connection') {
        downloadOutput.innerHTML = downloadOutput.innerHTML + "<p class='connection'>" + data + "</p>";
    }
    else if (type == 'big') {
        downloadOutput.innerHTML = downloadOutput.innerHTML + "<p class='big'>" + data + "</p>";
    }
    else {
        downloadOutput.innerHTML = downloadOutput.innerHTML + "<p>" + data + "</p>";
        ipc.send('resize', 0)
    }
    document.getElementById('scroll').scrollTop = downloadOutput.scrollHeight;
}


function calculateSIID(data) {
    // Turn the bytes transmitted from the station into a human readable and idenifiable SIID (SI Number)

    siid = parseInt(data[0] + data[1] + data[2] + data[3], 16);
    // SI Card 5
    if ((data[0] == 0x00) && (data[1] == 0x01)) {
        siid = siid - 65536;
    }
    else if ((data[0] == 0x00) && (data[1] == 0x02)) {
        siid = siid + 68928;
    }
    else if ((data[0] == 0x00) && (data[1] == 0x03)) {
        siid = siid + 103392;
    }
    else if ((data[0] == 0x00) && (data[1] == 0x04)) {
        siid = siid + 137856;
    }
    else if ((siid >= 3000000) && (siid <= 3999999)) {
        siid = siid - 0;
    }
    //SI Card 6
    else if ((siid >= 500000) && (siid <= 999999)) {
        siid = siid - 0;
    }
    //SI Card 8 + comCard Up
    else if ((siid >= 35554432) && (siid <= 36554431)) {
        siid = siid - 33554432;
    }
    //SI pCard
    else if ((siid >= 71180864) && (siid <= 72108863)) {
        siid = siid - 67108864;
    }
    //SI tCard
    else if ((siid >= 106663296) && (siid <= 107663295)) {
        siid = siid - 100663296;
    }
    //SI fCard
    else if ((siid >= 248881024) && (siid <= 249881023)) {
        siid = siid - 234881024;
    }
    // SI Card 10+
    else if ((siid >= 258658240) && (siid <= 261658239)) {
        siid = siid - 251658240;
    }

    return siid
}


function calculateTime(startRaw, finishRaw) {
    // Calculate time taken from start tofinish from the raw start and finish times

    timeRaw = finishraw - startRaw;
    timeMinutes = (timeRaw - (timeRaw % 60)) / 60;
    timeSeconds = timeRaw % 60;
    return [timeMinutes, timeSeconds, timeRaw];

}


function displayControlPunch(controlCode, time) {
    // Display Control Code and Time of punch in a human readable format

    timeHour = (time - (time % 3600)) / 3600;
    timeMinutes = ((time % 3600) - (time % 60)) / 60;
    timeSeconds = time % 60;
    output(controlCode + " - " + timeHour + ":" + timeMinutes + ":" + timeSeconds);

}


function getName(personalData) {
    // Get name from the personal data on the card
    // Data is separated by colons, first name then surname; so two colons

    var name = "";
    colonCounter = 0

    for (character of personalData) {
        if (character != 0x3B && colonCounter <= 1) {
            name = name + String.fromCharCode(character)
        }
        else if (character == 0x3B && colonCounter <= 1) {
            name = name + " "
            colonCounter++;
        }
        else {
            return name
            break
        }
    }
}


function processCard10Punches(data, blockNumber) {
    // Process the raw data inn Blocks 4-7 on Card10+ into readable data
    // CRC Check, read the punches, finish off

    if (parseInt(data[134].toString(16) + data[135].toString(16), 16) == parseInt(crc.compute(data.slice(1, 134)).toString(16), 16)) {

        var position = 6;
        while (position != 134 && data[position + 1] != 0xEE && data[position + 2] != 0xEE) {
            time = parseInt(data[position + 1].toString(16) + data[position + 2].toString(16), 16);
            controlCode = parseInt(data[position + 1])

            displayControlPunch(controlCode, time)

            position = position + 4;
        }
        if (blockNumber == 7) {
            currentDownload.complete = true;
            diplayControlPunch("F", finishTime)
            port.write(si.beep)
        }
        else {
            if (position != 134) {
                downloadComplete = true;
                displayControlPunch("F", finishTime)
                port.write(si.beep)
            }
            else {
                if (blockNumber == 4) {
                    port.write(card10.readBlock5)
                }
                else if (blockNumber == 5) {
                    port.write(card10.readBlock6)
                }
                else if (blockNumber == 6) {
                    port.write(card10.readBlock7)
                }
            }
        }
    }
    else {
        output("Error: Problem with data transmission - Please Re-insert Card", 'error')
    }
}


function dataTranslation(serialData) {
    // Turn raw data into times
    // Each type for packet is a seperate If and all have a CRC check

    // If Card5 just been inserted
    if ((serialData[0] == 0x02) && (serialData[1] == 0xE5)) { //If SI 5 inserted send signal to read SI5 data
        if (parseInt(serialData[9].toString(16) + serialData[10].toString(16), 16) == parseInt(crc.compute(serialData.slice(1, 9)))) {

            siid = calculateSIID(serialData.slice(5, 9))
            port.write(card5.read)

        }
        else {
            output("Error: Problem with data transmission - Please Re-insert Card", 'error')
        }
    }

    // If Card10+ just been inserted
    else if ((serialData[0] == 0x02) && (serialData[1] == 0xE8)) {//If SI 10+ inserted send signal to read SI10 data
        if (parseInt(serialData[9].toString(16) + serialData[10].toString(16), 16) == parseInt(crc.compute(serialData.slice(1, 9)))) {

            siid = calculateSIID(serialData.slice(5, 9))
            port.write(card10.readBlock0)

        }
        else {
            output("Error: Problem with data transmission - Please Re-insert Card", 'error')
        }
    }

    // Read block of Card 5 data
    else if ((serialData[0] == 0x02) && (serialData[1] == 0xB1) && (serialData[2] == 0x82) && (serialData.length == 136)) {

        if (parseInt(serialData[133].toString(16) + serialData[134].toString(16), 16) == parseInt(crc.compute(serialData.slice(1, 133)).toString(16), 16)) {

            port.write(si.beep)
            startTime = parseInt(serialData[card5.startByte1].toString(16) + serialData[card5.startByte2].toString(16), 16);
            finishTime = parseInt(serialData[card5.finishByte1].toString(16) + serialData[card5.finishByte2].toString(16), 16);
            time = calculateTime(startTime, finishTime)

            output("(" + siid + ")" + " - " + time[0] + ":" + time[1], 'big')
            displayControlPunch("S", startTime)

            var position = 38;
            var blockPosition = 0;
            while (serialData[position] != 0x00 && position != 130 && serialData[position + 1] != 0xEE) {

                time = parseInt(serialData[position + 1].toString(16) + serialData[position + 2].toString(16), 16);
                controlCode = parseInt(serialData[position])
                displayControlPunch(controlCode, time)

                if (blockPosition < 4) {
                    position = position + 3;
                    blockPosition++;
                }
                else {
                    position = position + 4;
                    blockPosition = 0
                }

            }
            displayControlPunch("F", finishTime)
        }
        else {
            output("Error: Problem with data transmission - Please Re-insert Card", 'error')
        }
    }

    // Read Block 0 of Card 10+ data
    else if ((serialData[0] == 0x02) && (serialData[1] == 0xEF) && (serialData[2] == 0x83) && (serialData[5] == 0x00) && (serialData.length == 137)) {
        if (parseInt(serialData[134].toString(16) + serialData[135].toString(16), 16) == parseInt(crc.compute(serialData.slice(1, 134)).toString(16), 16)) {

            startTime = parseInt(serialData[card10.startByte1].toString(16) + serialData[card10.startByte2].toString(16), 16);
            finishTime = parseInt(serialData[card10.finishByte1].toString(16) + serialData[card10.finishByte2].toString(16), 16);
            time = calculateTime(startTime, finishTime)

            var name = getName(serialData.slice(38, 133))

            output(name + " (" + siid + ")" + " - " + time[0] + ":" + time[1], 'big')
            displayControlPunch("S", startTime)

            port.write(card10.readBlock4)
            downloadComplete = false;

        }
        else {
            output("Error: Problem with data transmission - Please Re-insert Card", 'error')
        }
    }

    // Read Block 4 of Card 10+ data
    else if ((serialData[0] == 0x02) && (serialData[1] == 0xEF) && (serialData[2] == 0x83) && (serialData[5] == 0x04) && (serialData.length == 137)) {
        processCard10Punches(serialData, 4)
    }

    // Read Block 5 of Card 10+ data
    else if ((serialData[0] == 0x02) && (serialData[1] == 0xEF) && (serialData[2] == 0x83) && (serialData[5] == 0x05) && (serialData.length == 137)) {
        processCard10Punches(serialData, 5)
    }

    // Read Block 6 of Card 10+ data
    else if ((serialData[0] == 0x02) && (serialData[1] == 0xEF) && (serialData[2] == 0x83) && (serialData[5] == 0x06) && (serialData.length == 137)) {
        processCard10Punches(serialData, 6)
    }

    // Read Block 57of Card 10+ data
    else if ((serialData[0] == 0x02) && (serialData[1] == 0xEF) && (serialData[2] == 0x83) && (serialData[5] == 0x07) && (serialData.length == 137)) {
        processCard10Punches(serialData, 7)
    }

}


/* ------ Connect to Button and Use functions----- */

connect.addEventListener('click', function () {

    // Close Port if Open
    if (connect.textContent = "Disconnect") {
        port.close();
        connect.textContent = "Connect";
    }

    // Open the port
    else {

        /* ----- Set up Port ----- */

        port = new SerialPort(document.getElementById('port-content').innerText, {
            baudRate: parseInt(document.getElementById('baud-content').innerText),
            dataBits: 8,
            stopBits: 1,
            parity: 'none',
            autoOpen: false
        })

        port.on('open', function () {
            output(portName + " Opened", 'connection');
        });

        port.on('close', function () {
            output(portName + " Closed", 'connection');
            connect.textContent = "Connect";

        });

        port.on('error', function (err) {
            output('Error: ' + err.message, 'error');
            connect.textContent = "Connect";
        })

        // Turn random lengths of packets in to one long readable full length

        var dataInList = []
        port.on('data', function (data) {
            for (byte of data) {
                if (byte == 0x00) {
                    dataInList.push(0x00);
                }
                else if (byte != 0x03) {
                    dataInList.push(byte);

                }
                else {
                    if (dataInList.length == 135 || dataInList.length == 136 || dataInList.length == 11) {
                        dataTranslation(dataInList);
                        dataInList = []
                    }
                    else {
                        dataInList.push(byte);
                    }
                }
            }
        });

        port.open()
        connect.textContent = "Disconnect";
    }
})