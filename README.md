# Crawler for finn.no, `gis bort|Spill og konsoll`
Create a file called `emailConfig.json` in root folder.

```json
{
    "targetEmail": "recipient@some.email",
    "host": "smtp.googlemail.com",
    "port": 587,
    "secureConnection": true,
    "requiresAuth": true,
    "domains": ["gmail.com", "googlemail.com"],
    "logger": true,
    "direct": true,
    "auth": {
        "user": "sender@some.gmail",
        "pass": "app password from gmail"
    }
}
```