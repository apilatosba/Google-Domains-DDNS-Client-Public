# Google-Domains-DDNS-Client
Donk

Just run the program, it will tell you what you are missing.  
There is no set up wizard rn.  
Might add it in the future.  


## Notes  
Only works with IPv6  
The program should run in the background all the time


## How to Build and Run  
Go to the directory where .csproj file is located at. Then run this command:  
```cmd
dotnet run
```
Note that you need dotnet sdk.  
Running the code from visual studio's run button won't work. Because current directory is set to where .exe file is when you run from visual studio but current directory is set to where .csproj file is when you run dotnet run. This is inconsistency i think. I wrote the program to work with dotnet run. But i think what visual studio does is more natural. It is relative to executable like it should be. I hope they fix dotnet CLI. 
