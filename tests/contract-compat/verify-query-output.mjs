import { readFile } from 'node:fs/promises';

const clientPath = new URL('./generated/deferred-client.ts', import.meta.url);
const generated = await readFile(clientPath, 'utf8');

const requiredFragments = [
  "export const searchMessages = async (messageSearchRequest: MessageSearchRequest",
  "method: 'QUERY'",
  'body: JSON.stringify(messageSearchRequest)',
];

const missing = requiredFragments.filter((fragment) => !generated.includes(fragment));
if (missing.length > 0) {
  console.error(`Deferred QUERY client lost required output: ${missing.join(', ')}`);
  process.exitCode = 1;
}
