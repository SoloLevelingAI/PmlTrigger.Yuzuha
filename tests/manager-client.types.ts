// Compile-only consumer checks. No manager process is started by this file.
import {invokeCli, type CliOptions, type OperationResult} from '../docs/contracts/manager-journey-client.js';

declare const options: CliOptions;
invokeCli('manager.contracts', {}, options);
invokeCli('evars.preview', {installationRoot:'C:/Fixtures/AM', product:'AM', runtimeRoot:'C:/Fixtures/Runtime', profile:'AM', framework:'net35'}, options);
invokeCli('launch.profile.preview', {expectedRevision:'revision', profile:{environment:{PROJECT:'EXPLICIT'}}}, options);
invokeCli('connection.test', {}, {executable:'Yuzuha.Manager.Cli.exe', context:{processId:123, expectedProcessStartTimeUtcTicks:'638975584001234567', expectedModel:'DESIGN'}});
// @ts-expect-error Process identity ticks must retain Int64 decimal precision.
invokeCli('connection.test', {}, {executable:'Yuzuha.Manager.Cli.exe', context:{expectedProcessStartTimeUtcTicks:638975584001234567}});
// @ts-expect-error Explicit grant flags are booleans.
invokeCli('evars.apply', {planId:'id', sha256:'hash'}, {executable:'Yuzuha.Manager.Cli.exe', context:{allowWrite:'true'}});
// @ts-expect-error Unknown operation IDs are not part of the actual registry.
invokeCli('manager.guessed-operation', {}, options);
// @ts-expect-error Actual enum values are preserved from the registered schema.
invokeCli('evars.preview', {installationRoot:'C:/Fixtures', product:'UNKNOWN', runtimeRoot:'C:/Fixtures/Runtime', profile:'AM', framework:'net35'}, options);
// @ts-expect-error Environment values are strings, not unconstrained objects.
invokeCli('launch.profile.preview', {expectedRevision:'revision', profile:{environment:{PROJECT:123}}}, options);
declare const result: OperationResult;
// @ts-expect-error Output data has no registered operation-specific schema.
result.data.guessedProperty;
