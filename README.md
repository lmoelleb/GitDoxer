# KiCadDoxer
Generate SVG from KiCad files

# Using
https://kicaddoxer.azurewebsites.net/?url={your_url_encoded_url_here}

or alternatively, if your file is on github you can use:
https://kicaddoxer.azurewebsites.net/github/{github_user}/{github_project}/{branch}/{path_to_sch_file}

Please see the Issues list for limitations. If you need a specific feature, leave a note on the issue, or create a pull request.

# Contributing
The code is written in .NET Core. I tried using Node.JS to have the lowest barrier to contributing and hosting.
While I could live with most of the JavaScript limitation due to the simplicity of the task there was one I could not accept:
lack of proper support for asynchronious calls. As far as I could find out, async had to be implemented with callbacks which impacts
error handling and code readability to a level I am not willing to accept.

I tried switching to TypeScript, but something was wrong with my Visual Studio 2017 installation - 
it would not compile a newly created TypeScript project with no changes. Surely this could be fixed, but I had already spend
too much time getting tools to work, and too little implementing the SVG generator so I had to cut my losses and move to an environment
where I am more productive.

In general the code is written extremely fast, not taking advanced architecture into account - it doesn't need to really as it is a
small project.

Sure, there should be unit tests - I know, I write them at work. And if you are willing to pay
me, I'll write them here as well. :) If you do not want to pay and you do want unit tests, there are two options:
1. Write them and contribure
2. Wait until I have to refactor something - then I'll probably write the unit tests for it first.
