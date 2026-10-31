import {readFileSync,readdirSync} from 'node:fs';
import {join} from 'node:path';
function files(root){return readdirSync(root,{withFileTypes:true}).flatMap(e=>e.isDirectory()?['bin','obj'].includes(e.name)?[]:files(join(root,e.name)):e.name.endsWith('.cs')?[join(root,e.name)]:[]);}
const issues=[];
for(const file of files('apps/desktop')){const text=readFileSync(file,'utf8');const fixture=file.split(/[\\/]/).includes('Verification');if(/using RibbonStudio.Backend|WorkspaceFiles/.test(text)||!fixture&&/File\.(WriteAll|Replace|Move).*Customization/.test(text))issues.push(file+': UI must call HTTP rather than backend services');}
for(const file of files('apps/backend'))if(/System.Windows|PresentationFramework|RibbonStudio\.MainWindow/.test(readFileSync(file,'utf8')))issues.push(file+': backend must not depend on WPF');
const history=readFileSync('apps/desktop/Features/Editing/EditHistory.cs','utf8');if(/Json\.|CaptureHistory|HistoryFrame|\.Data/.test(history))issues.push('edit history must retain operations, not full document snapshots');
if(issues.length)throw new Error(issues.join('\n'));console.log('Feature boundaries and incremental edit history: PASS');
