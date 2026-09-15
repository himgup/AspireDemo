// Writes src/env.js so the browser bundle can read the Order API URL that
// Aspire injects into this process via the "services__orderapi__http__0" env var
// (Aspire's standard convention for a referenced resource's endpoint).
const fs = require('fs');
const apiUrl = process.env['services__orderapi__http__0'] || 'http://localhost:5100';
fs.writeFileSync(
  __dirname + '/src/env.js',
  `window.__ORDER_API_URL__ = ${JSON.stringify(apiUrl)};\n`
);
console.log('[generate-env] Order API URL ->', apiUrl);
