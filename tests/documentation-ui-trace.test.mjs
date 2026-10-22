import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync, mkdirSync, writeFileSync, readFileSync, rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join, resolve} from 'node:path';
import {execFileSync} from 'node:child_process';

function trace(source, xaml, generated = '') {
  const root = mkdtempSync(join(tmpdir(), 'yuzuha-ui-trace-'));
  try {
    writeFileSync(join(root, 'View.cs'), source);
    writeFileSync(join(root, 'View.xaml'), xaml);
    const sources = [join(root, 'View.cs')];
    if (generated) {
      mkdirSync(join(root, 'generated'));
      writeFileSync(join(root, 'generated/Model.g.cs'), generated);
      sources.unshift(join(root, 'generated/Model.g.cs'));
    }
    writeFileSync(join(root, 'manifest.json'), JSON.stringify({projects: [{name: 'Fixture', sources, references: [], defines: ''}], xaml: [join(root, 'View.xaml')]}));
    execFileSync('dotnet', [resolve('tools/Documentation/bin/Release/net10.0/Documentation.dll'), '--ui-trace', root, join(root, 'manifest.json'), join(root, 'trace.json')], {windowsHide: true, stdio: 'pipe'});
    return JSON.parse(readFileSync(join(root, 'trace.json'), 'utf8'));
  } finally { rmSync(root, {recursive: true, force: true}); }
}
const view = body => `<Window xmlns="urn:fixture" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:Demo" x:Class="Demo.View">${body}</Window>`;
const node = (graph, suffix) => graph.nodes.find(n => n.id.endsWith(suffix));

test('XAML event resolves exact overload and source coordinates; dynamic call stays unresolved', () => {
  const g = trace('namespace Demo; class Service { public void Go(int n) {} public void Go(string s) {} } class View { Service service = new(); void Start(object s, object e) { service.Go(1); dynamic d = service; d.Go(1); } }', view('<Button x:Name="GoButton" Click="Start"/>'));
  const start = node(g, 'M:Demo.View.Start(System.Object,System.Object)');
  const chosen = node(g, 'M:Demo.Service.Go(System.Int32)');
  assert.equal(g.entries[0].target, start.id);
  assert.equal(g.entries[0].control, 'GoButton');
  assert.ok(g.edges.some(e => e.from === start.id && e.to === chosen.id && e.kind === 'call'));
  assert.ok(!g.edges.some(e => e.to.endsWith('M:Demo.Service.Go(System.String)')));
  assert.ok(g.boundaries.some(b => b.from === start.id && b.expression === 'd.Go(1)'));
  assert.equal(chosen.file, 'View.cs'); assert.equal(chosen.line, 1);
});

test('Command follows explicit runtime DataContext and delegate registration, separating CanExecute', () => {
  const g = trace('using System; namespace Demo; class View {} class Command { public Command(Action execute, Func<bool> canExecute) {} } class Model { public Command SaveCommand { get; } = new Command(Save, CanSave); static void Save() { Store(); } static bool CanSave() => true; static void Store() {} }', view('<Window.DataContext><local:Model/></Window.DataContext><Button Command="{Binding SaveCommand}"/>'));
  assert.equal(g.entries[0].resolution, 'resolved');
  assert.equal(g.entries[0].target, node(g, 'P:Demo.Model.SaveCommand').id);
  assert.ok(g.edges.some(e => e.kind === 'delegate-registration:execute' && e.to === node(g, 'M:Demo.Model.Save').id));
  assert.ok(g.edges.some(e => e.kind === 'delegate-registration:canExecute' && e.to === node(g, 'M:Demo.Model.CanSave').id));
});

test('property write follows actual generated setter to OnChanged; callbacks are not guessed by name', () => {
  const g = trace('namespace Demo; class View { Model model = new(); void Start(object s, object e) { model.Name = "new"; } } partial class Model { partial void OnNameChanged(string s) { Store(); } void Store() {} void OnOtherChanged(string s) {} }', view('<Button Click="Start"/>'), 'namespace Demo; partial class Model { partial void OnNameChanged(string s); string name = ""; public string Name { get => name; set { name = value; OnNameChanged(value); } } }');
  const setter = node(g, 'M:Demo.Model.set_Name(System.String)');
  assert.equal(setter.generated, true);
  assert.equal(node(g, 'M:Demo.Model.OnNameChanged(System.String)').file, 'View.cs');
  assert.ok(g.edges.some(e => e.kind === 'property-write' && e.to === setter.id));
  assert.ok(g.edges.some(e => e.from === setter.id && e.to === node(g, 'M:Demo.Model.OnNameChanged(System.String)').id));
  assert.ok(!g.edges.some(e => e.to.endsWith('M:Demo.Model.OnOtherChanged(System.String)')));
});

test('interface implementations remain candidates, including explicit implementation', () => {
  const g = trace('namespace Demo; interface IService { void Run(); } class Real : IService { public void Run() {} } class Fake : IService { void IService.Run() {} } class View { IService service = new Real(); void Start(object s, object e) { service.Run(); } }', view('<Button Click="Start"/>'));
  const candidates = g.edges.filter(e => e.from.endsWith('M:Demo.IService.Run'));
  assert.equal(candidates.length, 2);
  assert.ok(candidates.every(e => e.kind === 'possible-implementation'));
});

test('unknown/template DataContext, ElementName binding and missing generator output are explicit boundaries', () => {
  const g = trace('namespace Demo; class View {} class Model { public object SaveCommand { get; } = new(); }', view('<Window.DataContext><local:Model/></Window.DataContext><StackPanel DataContext="{Binding Selected}"><Button Command="{Binding SaveCommand}"/></StackPanel><DataTemplate><Button Command="{Binding SaveCommand}"/></DataTemplate><Button Command="{Binding SaveCommand, ElementName=Other}"/>'));
  assert.equal(g.entries.length, 3);
  assert.ok(g.entries.every(e => e.resolution === 'unresolved' && e.reason));
  const missing = trace('namespace Demo; class View {} class ObservablePropertyAttribute : System.Attribute {} class Model { [ObservableProperty] string name = ""; void OnNameChanged(string s) {} }', view('<Button Command="{Binding SaveCommand}"/>'));
  assert.ok(missing.boundaries.some(b => b.kind === 'generator-input'));
  assert.ok(!missing.edges.some(e => e.to.includes('OnNameChanged')));
});

test('explicit TwoWay binding starts at the actual setter; virtual dispatch is not a definite implementation', () => {
  const g = trace('namespace Demo; class View {} class Base { public virtual void Store() {} } class Model : Base { string name = ""; public string Name { get => name; set { name = value; OnNameChanged(); } } void OnNameChanged() { Store(); } }', view('<Window.DataContext><local:Model/></Window.DataContext><TextBox Text="{Binding Name, Mode=TwoWay}"/>'));
  assert.equal(g.entries[0].target, node(g, 'M:Demo.Model.set_Name(System.String)').id);
  assert.equal(g.entries[0].member, 'Text');
  assert.ok(g.edges.some(e => e.kind === 'virtual-dispatch' && e.to === node(g, 'M:Demo.Base.Store').id));
  assert.ok(g.boundaries.some(b => b.kind === 'virtual-dispatch'));
});

test('event raise and named subscription connect to OnChanged without treating unsubscribe as execution', () => {
  const g = trace('using System; namespace Demo; class View {} class Model { public event Action? Changed; void Subscribe() { Changed += OnChanged; Changed -= OnChanged; } void Update() { Changed?.Invoke(); } void OnChanged() { Store(); } void Store() {} }', view(''));
  const event = node(g, 'E:Demo.Model.Changed');
  assert.ok(g.edges.some(e => e.from === node(g, 'M:Demo.Model.Update').id && e.to === event.id && e.kind === 'event-raise'));
  assert.ok(g.edges.some(e => e.from === event.id && e.to === node(g, 'M:Demo.Model.OnChanged').id && e.kind === 'possible-event-handler'));
  assert.ok(g.boundaries.some(b => b.kind === 'event-unsubscription'));
});

test('XAML assembly qualification never binds to a same-name type in another assembly', () => {
  const g = trace('namespace Demo; class View {} class Model { public object SaveCommand { get; } = new(); }', view('<Window.DataContext><local:Model/></Window.DataContext><Button Command="{Binding SaveCommand}"/>').replace('clr-namespace:Demo', 'clr-namespace:Demo;assembly=Other'));
  assert.equal(g.entries[0].resolution, 'unresolved');
  assert.equal(g.entries[0].target, null);
});

test('page discovery keeps nested tabs, shared handlers and initially disabled unwired actions', () => {
  const g = trace('namespace Demo; class View { void Shared(object s, object e) {} void Go(object s, object e) {} }', view('<Button Click="Shared"/><TabControl x:Name="Tabs"><TabItem Header="Launch"><Button Click="Go"/></TabItem><TabItem Header="Planned"><TabControl x:Name="Nested"><TabItem Header="Review"><Button Content="Migrate" IsEnabled="False"/></TabItem></TabControl></TabItem></TabControl>'));
  const pages = new Map(g.pages.map(p => [p.name, p]));
  assert.equal(pages.get('Launch').kind, 'page');
  assert.equal(pages.get('Planned').kind, 'container');
  assert.equal(pages.get('Review').parentId, pages.get('Planned').id);
  assert.equal(g.entries.find(e => e.expression === 'Go').pageId, pages.get('Launch').id);
  const shared = g.entries.find(e => e.expression === 'Shared');
  assert.equal(g.pages.find(p => p.id === shared.pageId).kind, 'shared');
  const pending = g.entries.find(e => e.control === 'Migrate');
  assert.equal(pending.pageId, pages.get('Review').id);
  assert.equal(pending.initiallyDisabled, true);
  assert.equal(pending.resolution, 'unresolved');
  assert.ok(pending.reason.includes('Click'));
  assert.match(pending.sourceSha256, /^[a-f0-9]{64}$/);
});

test('object construction is a real edge and view constructors remain shared lifecycle entries', () => {
  const g = trace('namespace Demo; class View { public View() {} void Start(object s, object e) { var service = new Service { Value = 1 }; service.Run(); } } class Service { public int Value; public Service() {} public void Run() {} }', view('<Button Click="Start"/>'));
  assert.ok(g.edges.some(e=>e.from.endsWith('M:Demo.View.Start(System.Object,System.Object)')&&e.to.endsWith('M:Demo.Service.#ctor')&&e.kind==='constructor'));
  const lifecycle=g.entries.find(e=>e.member==='C# Constructor');
  assert.equal(g.pages.find(p=>p.id===lifecycle.pageId).kind,'shared');
});
