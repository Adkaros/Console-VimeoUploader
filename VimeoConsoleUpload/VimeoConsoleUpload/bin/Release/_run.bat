// pram 1 = group number
// pram 2 = video start folder
// pram 3 = video end folder
// pram 4 = activation "dance" "sing" "listen"
// pram 5 = ip of pc running WAMP normally should be local "127.0.0.1:8080"
PING 1.1.1.1 -n 1 -w 1000>nul
start VimeoConsoleUpload "1" "C:\Bravo/destination" "C:\Bravo/uploaded" "dance" "127.0.0.1:8080"
