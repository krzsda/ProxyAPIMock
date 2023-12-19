- Run via ProxyApiMock launchsettings or build release and open from the binaries.

- Every Service in appsettings.json requires a url to which real API calls will be forwarded if no mocked 
request is found and will look for a MockedRequest/{Service.Name}.json file for the mocked requests. 

- Set endpoint in MockedRequest/{Service.Name}.json for the application to check 
if the request is made to that endpoint and set mockparams for a parameter and value to be searched in the request body.
If all parameters are found and have correct value then the matching item from MockedRequest/{Service.Name}.json is returned
- as in mocked request body and headers are returned and no external API call is made.

- Each service logs, and all real requests and responses - are writtten to files for each Service separatly. 
file AppContext.BaseDirectory/Logs/{Service.Name}/timestamp_of_request.json and ProxyApiHandler.log

- Main service logs to file AppContext.BaseDirectory/Logs/ProxyApiMock.log