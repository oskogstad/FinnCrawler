const html2json = require('html2json').html2json;
const nodemailer = require("nodemailer");
const traverse = require('traverse');
const https = require('https');
const fs = require('fs');

const finnFreeGamesURL = 'https://www.finn.no/bap/forsale/search.html?category=0.93&search_type=SEARCH_ID_BAP_FREE&sort=1&sub_category=1.93.3905'; 
const finnAdBaseURL = 'https://www.finn.no/bap/forsale/ad.html?finnkode=';
const emailConfigFileName = 'emailConfig.json';
const finnCodesFileName = 'finnCodes.json';

const minimumWaitTimeInMs = 60000;

let emailConfig = {};
let finnCodes = {};

if(!fs.existsSync(emailConfigFileName)) {
    console.log("config file missing, bailing");
    process.exit();
}

let emailConfigData = fs.readFileSync(emailConfigFileName);
emailConfig = JSON.parse(emailConfigData);

if(fs.existsSync(finnCodesFileName)) {
    let finnCodesData = fs.readFileSync(finnCodesFileName);
    finnCodes = JSON.parse(finnCodesData);
}

function GetAllAdsAndEmailNewONes() {
    https.get(finnFreeGamesURL, (resp) => {
        let data = '';
        resp.on('data', (chunk) => {
            data += chunk;
        });
        
        resp.on('end', () => {
            let finnResultJson = html2json(data);
            let tempFinnCodes = [];

            traverse(finnResultJson).forEach(function (jsonElement) {
                if(this.key==='data-finnkode') {
                    tempFinnCodes.push(jsonElement);
                }
            });

            let urlsToBeEmailed = [];
            tempFinnCodes.forEach((finnCode) => {
                if(!(finnCode in finnCodes)) {
                    finnCodes[finnCode] = Date.now();
                    urlsToBeEmailed.push(finnAdBaseURL + finnCode);
                }
            });

            fs.writeFileSync(finnCodesFileName, JSON.stringify(finnCodes));

            if (urlsToBeEmailed.length) {
                console.log("Sending email");

                let plural = urlsToBeEmailed.length > 1;
                let newAdsText = `Ny${ plural ? 'e' : '' } annonse${ plural ? 'r' : '' }`;
                let htmlBody = `${newAdsText}: <br />`;
                
                urlsToBeEmailed.forEach((element) => {
                    htmlBody += element + "<br />";
                });

                let mailOptions = {
                    from: `"Find Crawler" ${emailConfig.auth.user}`,
                    to: emailConfig.targetEmail,
                    subject: `${newAdsText}, gis bort|Spill og konsoll`,
                    html: htmlBody
                };
                
                let transporter = nodemailer.createTransport(emailConfig);

                transporter.sendMail(mailOptions, (err, success) => {
                    if(err) {
                        console.log(err);
                    }
                } );
            }
            else {
                console.log("No new ads");
            }
        });

    }).on("error", (err) => {
        console.log("Error: " + err.message);
    });

    let randomTimeToAddInMs = Math.floor(Math.random() * minimumWaitTimeInMs);
    let totalWaitTimeInMs = minimumWaitTimeInMs + randomTimeToAddInMs;

    console.log(`Waiting ${totalWaitTimeInMs/1000} seconds ...`);
    setTimeout(GetAllAdsAndEmailNewONes, totalWaitTimeInMs);
}

GetAllAdsAndEmailNewONes();
