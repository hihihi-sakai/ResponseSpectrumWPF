Response spectrum
====

This windows program calculate response spectrum from such as seismic acceleration, using Nigam Jennings method.

## Description
Feature
- calculate response spectrum using Nigam /Jennings method
- set dampings
- set ths number of plot as Hz or sec
- set plot type to liner or log
- GUI by WPF
- show result as graph
- output result as excel, html, csv and text

GUI is in Japanese...

Prepare data one value(acceleration) per one line.

% [m/s2] ----> '%'' is comment line
0.00000000
0.00005533
0.00016548
0.00025011
0.00027940
...

## Demo


## Requirement
Visual studio 2017 ,C#, .net framework 4, WPF, Excel 2010 or higher to show Excel output.
using library
- [Closed XML](https://github.com/ClosedXML/ClosedXML) to create Excel output
- [ExcelNumberFormat](https://github.com/andersnm/ExcelNumberFormat)
- [OxyPlot](http://www.oxyplot.org/) to make graph
- [jQurey](https://jquery.org/) and [bootstrap](https://getbootstrap.com/) to create html output


Compile on Mono is not tried.

## Usage
Compile "WaveProcessWPF" project and run.

## Install
Not ready

## Licence
MIT

## Author
Hidetoshi Sakai



