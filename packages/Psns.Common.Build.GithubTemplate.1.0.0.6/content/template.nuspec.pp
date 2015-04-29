<?xml version="1.0"?>
<package >
  <metadata>
    <id>$rootnamespace$</id>
    <title>$rootnamespace$</title>
    <authors>PSNS</authors>
    <owners>109.14</owners>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <description></description>
    <projectUrl>https://github.com/PSNS-IMF/[github project name here]</projectUrl>
    <licenseUrl>https://github.com/PSNS-IMF/[github project name here]/blob/master/LICENSE.md</licenseUrl>
    <releaseNotes>
      ## 1.0.0.0
      ### Features
      * Initial release
    </releaseNotes>
    <version>$$version$$</version>
    <tags>PSNS</tags>
  </metadata>
  <files>
    <file src="..\bin\$$configuration$$\$rootnamespace$.*" target="lib\net40\" />
    <file src="..\bin\$$configuration$$\net45\$rootnamespace$.*" target="lib\net45\" />
  </files>
</package>