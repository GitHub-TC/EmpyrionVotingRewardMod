# EmpyrionBackpackExtender

## Installation
Sie können diesen Mod direkt mit dem MOD-Manager von EWA (Empyrion Web Access) laden. <br/>
Ohne den EWA funktioniert der Mod (vermutlich) nur innerhalb des EmpyrionModHost

## Konfigurieren Sie Ihre Belohnungen
Nach der Installation und dem Start des Mods gibt es hier eine Beispielkonfiguration, die angepasst werden kann.
[SaveGamePath]\\Mods\\VotingReward\\Configuration.json

Hier muss auch der API-Schlüssel für den Zugriff auf https://empyrion-servers.com hinterlegt werden

## Verwendungszweck
Nach einer Abstimmung auf der Seite https://empyrion-servers.com kann der Spieler seine Belohnung mit der Teilabstimmung anfordern
* "\\votereward" gibt dir eine Belohnung
* "\\votelottery" spielt in der Lotterie mit Ihrer Stimme
* "\\voteforstat health" erhöht deine Gesundheit mit deiner Stimme
* "\\voteforstat stamina" erhöht deine Ausdauer mit deiner Stimme
* "\\voteforstat food" erhöht dein Essen mit deiner Stimme

Mit dem Befehl "\\vote help" kann der Spieler die möglichen Belohnungen und die Anzahl seiner Stimmen abrufen.

Konfigurationsparameter:
```
{0} = VotingApiServerKey
{1} = Player SteamID
{2} = Player Name
```

Konfiguration für top-games.net (PlayerName):
```
"ServerVotingHomepage": "https://top-games.net",
"GetUnclaimedVoteUrl" : "https://api.top-games.net/v1/votes/claim-username?server_token={0}&playername={2}",
"GetUnclaimedVoteMatch": ".*\"claimed\"\\s*:\\s*1\\s*,.*",
"ClaimedVoteUrl": "",
```

Konfiguration für top-games.net (SteamId) (scheint nicht zu funktionieren):
```
"ServerVotingHomepage": "https://top-games.net",
"GetUnclaimedVoteUrl" : "https://api.top-games.net/v1/votes/claim-steam?server_token={0}&steam_id={1}",
"GetUnclaimedVoteMatch": ".*\"claimed\"\\s*:\\s*1\\s*,.*",
"ClaimedVoteUrl": "",
```

# Empyrion Voting Reward Mod

## Installation
Your can direct load this mod with the EWA (Empyrion Web Access) MOD manager.<br/>
Without the EWA the mod works only within the EmpyrionModHost

## Config your rewards
After the installation and the start of the mod is here an example configuration which can be adapted.
[SaveGamePath]\\Mods\\VotingReward\\Configuration.json

Here also the API key must be deposited for the access to https://empyrion-servers.com

## Usage
After a vote on the page https://empyrion-servers.com the player can request his reward with the fractional vote
* "\\votereward" gives you a reward
* "\\votelottery" play in the lottery with your vote
* "\\voteforstat health" increase your health with you vote
* "\\voteforstat stamina" increase your stamina with you vote
* "\\voteforstat food" increase your food with you vote

With the command "\\vote help" the player can retrieve the possible rewards and his number of votes.

Configuration parameters:
```
{0} = VotingApiServerKey
{1} = Spieler SteamID
{2} = Spielername
```

Configuration for top-games.net (PlayerName):
```
"ServerVotingHomepage": "https://top-games.net",
"GetUnclaimedVoteUrl" : "https://api.top-games.net/v1/votes/claim-username?server_token={0}&playername={2}",
"GetUnclaimedVoteMatch": ".*\"claimed\"\\s*:\\s*1\\s*,.*",
"ClaimedVoteUrl": "",
```

Configuration for top-games.net (SteamID) (does not seem to be working):
```
"ServerVotingHomepage": "https://top-games.net",
"GetUnclaimedVoteUrl" : "https://api.top-games.net/v1/votes/claim-steam?server_token={0}&steam_id={1}",
"GetUnclaimedVoteMatch": ".*\"claimed\"\\s*:\\s*1\\s*,.*",
"ClaimedVoteUrl": "",
```
