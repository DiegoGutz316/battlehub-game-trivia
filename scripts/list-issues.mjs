import fs from 'node:fs';

const path = process.argv[2];
const data = JSON.parse(fs.readFileSync(path, 'utf8'));
for (const item of data) {
  const kind = item.pull_request ? 'PR' : 'ISSUE';
  console.log(`${kind} #${item.number} [${item.state}] ${item.title}`);
}
