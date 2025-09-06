## Local test

choco pack
choco install servy -s .
choco install servy -s . -y
choco uninstall servy -s .
choco push servy.1.0.0.nupkg --source https://push.chocolatey.org/

choco apikey --key="YOUR_API_KEY_HERE" --source="https://push.chocolatey.org/"

## Test
choco install servy -y
