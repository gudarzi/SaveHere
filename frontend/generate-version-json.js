import fs from 'fs';
import path from 'path';

// Get today's date in local time
const today = new Date();
const year = today.getFullYear();
const month = String(today.getMonth() + 1).padStart(2, '0'); // getMonth() is zero-based
const day = String(today.getDate()).padStart(2, '0');
const hours = String(today.getHours()).padStart(2, '0');
const minutes = String(today.getMinutes()).padStart(2, '0');

// Combine date, hour, and minute to create a unique version string
const formattedDate = `${year}.${month}.${day}`;
const versionString = `${formattedDate}.${hours}${minutes}`;

// Define the version object
const version = {
  current_version: versionString
};

// Path to the version.json file
const versionFilePath = path.join(process.cwd(), 'public', 'version.json');

// Write the version object to version.json
fs.writeFile(versionFilePath, JSON.stringify(version, null, 2), (err) => {
  if (err) {
    console.error('Error writing version.json:', err);
  } else {
    console.log('version.json created successfully:', version);
  }
});