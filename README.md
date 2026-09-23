# wk-tax-api
How to get started with agentic development with Wolters Kluwer CCH Axcess Tax APIs. This will be used in a presentation and lab from the *2026 CCH User Connections: User Conference*.

Developer Portal: https://developers.cchaxcess.com/


## Setting up the Environment File
A .env file will be used to hold your credentials or secrets.
In the Terminal window:

> cp env.template .env

Open the .env file and fill in the correct values from the Profile section in the Developer Portal.  

## Setting up Authentication
This has already been done for you using the following prompt. We will use this as a starting point for our other labs.

> I would like to build a dotnet core command line utility called `MyCCHTools` that will enable me to login using the authentication services API, OpenAPI3 files are located in `./swaggerFiles/AuthenticationServices.json`. This would use the values in the `.env` file, and if any values are missing from the file the user is prompted to enter the missing value. If the `IntegratorKey` is missing, the login should fail and report the configuration error. It should be assumed that this is an internal user unless specified in the `.env` file. Additionally, we are not supporting ADFS at this time. Please verify that the generated JSON parameters for properties in the request bodies matches the case of the examples in the swagger files, for example the parameters in the request body for the `/AuthService/v1.0/Authenticate` endpoint must be PascalCase. Once the login is successful, the token should be stored in a cache that lives for the life of the token.  
Once the auth token is successfully cached, I would like to be presented with options of actions. For now, those will be place holders to test the CLI and the option selection and navigation. Please use a library such as Spectre.Console to allow styling and better "look & feel" if there are more appropriate options for TUI design and handling, please suggest those.  
Errors need to print Exceptions to the terminal. If a web request response is unable to be parsed by the Json handler, then the response should be printed for diagnosis. Please do not print web service output, such as tokens or raw responses unless an error has occurred.


## Running MyCCHTools

The `MyCCHTools` console application authenticates against the Authentication Services API described by `swaggerFiles/AuthenticationServices.json`.

```bash
cp env.template .env
dotnet run --project src/MyCCHTools/MyCCHTools.csproj
```

`INTEGRATOR_KEY` is required in `.env`; the application fails with a configuration error rather than prompting for it. Missing username, password, user SID, or realm (account number) values are prompted for at login. `CCH_IS_INTERNAL` defaults to `true`. ADFS authentication is not supported in this lab and no ADFS property is sent.

The request body is serialized with PascalCase property names matching the OpenAPI examples: `UserName`, `UserSid`, `Password`, `Realm`, and `IsInternal`. After a successful login, the token remains in an in-memory cache until its reported expiration or until the process exits. The menu currently contains placeholder actions for testing navigation.


## Lab Instructions
- login to GitHub
- clone this repo
- profit
