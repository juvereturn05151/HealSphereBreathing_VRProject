// Local operator tool. No provider calls, no secret output, no library reset.
import { createHash } from 'node:crypto';
import { configuration } from '../src/config.js';
import { openLibrary } from '../src/library.js';
const [command = 'list', id, consent] = process.argv.slice(2);
if (!['list', 'show', 'publish', 'retire'].includes(command)) throw new Error('Use: list | show ID | publish ID --approve-shared | retire ID --confirm-retire');
const options = configuration();
if (!options.token || options.token.length < 32) throw new Error('LOCAL_CAPABILITY_REQUIRED');
const owner = createHash('sha256').update(options.token).digest('hex');
const library = await openLibrary({ ...options, sharedCollection: true, allowUnreviewed: options.reviewMode === 'none' });
try {
  if (command === 'publish') {
    if (consent !== '--approve-shared') throw new Error('EXPLICIT_SHARED_APPROVAL_REQUIRED');
    library.publish(id, owner);
  } else if (command === 'retire') {
    if (consent !== '--confirm-retire') throw new Error('EXPLICIT_RETIRE_CONFIRMATION_REQUIRED');
    library.retire(id, owner);
  }
  const compact = x => x && ({ id: x.id, displayName: x.displayName, category: x.category, source: x.source,
    review: x.review, visibility: x.visibility, bytes: x.bytes, validation: x.validation, task_id: x.task_id });
  console.log(JSON.stringify(command === 'list' ? library.list(owner).map(compact) : compact(library.get(id, owner)) ?? { retired: true }, null, 2));
} finally { library.close(); }
