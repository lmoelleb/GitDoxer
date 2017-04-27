# KiCadDoxer
Generate SVG from KiCad schematic files.

While you can export SVG files from within KiCad it is a manual operation without any supported automation possibilities at this time. If you are checking into source control and need updated SVG files that match the latest checked in version then it is easy to forget.

KiCadDoxer is a hosted solution - if your schematic file is available online you can generate the SVG simply by entering the correct URL into your browser or any other HTTP Client - for example [curl](https://curl.haxx.se/).

## Using
Get the URL of your KiCad schematic (.sch) file and insert it in the following address:
```
https://kicaddoxer.azurewebsites.net/?url={your_url_encoded_url_here}
```
or alternatively; if your file is on github you can use:
```
https://kicaddoxer.azurewebsites.net/github/{github_user}/{github_project}/{branch}/{path_to_sch_file}
```
You must have the library cache file (the file with the name \*-cache.lib) available at the same location as the schematic you are rendering. For example, if you want to render the file
```
https://github.com/trainiot/Hardware/blob/master/Spi2Dcc/DccGenerator.sch
```
then the library cache must be available at:
```
https://github.com/trainiot/Hardware/blob/master/Spi2Dcc/SpiDcc-cache.lib
```
### Using in GitHub markdown (\*.md files)
While you can use the image link directly in [**GitHub** Pages](https://pages.github.com/) you need to modify the links slightly to work with markdown files - for example in README.md files like this one.

When used in a GitHub markdown file, **the url must end with ".svg"**
The easiest approach to ensure this is to add a dummy query parameter at the end of the URL. So if you have the URL
```
https://github.com/trainiot/Hardware/blob/master/Spi2Dcc/DccGenerator.sch
```
change it to
```
https://github.com/trainiot/Hardware/blob/master/Spi2Dcc/DccGenerator.sch?.svg
```
### Optional query parameters
Query parameters are not case sensitive.
- HiddenPins
  - Hide  
Do not show hidden pins (default).
  - Show  
Show hidden pins
  - ShowIfConnectedToWire  
Only show hidden pins if they are connected by a wire (a direct connection to a component will not work) *and* the pin has a name that differs from the component name. In the example below this can be seen where U402A displays the hidden pins connected to power and bypass capacitor while U402B hides the hidden pins. It is specifically useful for hidden pins with long leads.
- PinNumbers
  - Show  
Show pin numbers (default). This will not show pin numbers if they are explicitly hidden in the component library.
  - Hide
Always hide pin numbers.
- PrettyPrint
  - Yes  
  Output the SVG file with indentation making it easier to read.
  - No
  Output the SVG file without indentation (default).
- AddClasses
  - Yes  
  Include CSS classes on the SVG elements.
  - No
  Do not include CSS classes on the SVG elements (default).
  

## Example:
This drawing is generated from the following link:
```
https://kicaddoxer.azurewebsites.net/github/trainiot/Hardware/master/Spi2Dcc/DccStateMachine.sch?hiddenpins=ShowIfConnectedToWire
```
![State Machine schematic](https://kicaddoxer.azurewebsites.net/github/trainiot/Hardware/master/Spi2Dcc/DccStateMachine.sch?hiddenpins=ShowIfConnectedToWire&.svg)
The full markup syntax used is:
```
![State Machine schematic](https://kicaddoxer.azurewebsites.net/github/trainiot/Hardware/master/Spi2Dcc/DccStateMachine.sch?hiddenpins=ShowIfConnectedToWire&.svg)
```

## Limitations and known issues
Please see the Issues list for limitations. If you need a specific feature, leave a note on the issue, or create a pull request.

## Contributing
The code is written in .NET Core. I tried using Node.JS to have the lowest barrier to contributing and hosting.
While I could live with most of the JavaScript limitation due to the simplicity of the task there was *one* I could not accept:
lack of proper support for asynchronious calls. As far as I could find out async had to be implemented with callbacks. This impacts
error handling and code readability to a level I am not willing to accept.

I tried switching to TypeScript, but something was wrong with my Visual Studio 2017 installation - 
it would not compile a newly created TypeScript project with no changes. Surely this could be fixed, but I had already spend
too much time getting tools to work, and too little implementing the SVG generator so I had to cut my losses and move to an environment
where I am more productive.

In general this is a small project, so don't expect to find any showcase architecture here. The furtherst architecture will be pushed is a bit of dependency injection.

Sure, there should be unit tests - I know, I write them at work. And if you are willing to pay me, I'll write them here as well. :) If you do not want to pay and you do want unit tests, there are two options:
1. Write them and contribute
2. Wait until I have to refactor something - then I'll probably write the unit tests for it first.
