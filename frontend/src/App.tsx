import { useState } from 'react';
import './App.css'
import DownloadedFilesList from './components/DownloadedFilesList'
import QueueItemsList from './components/QueueItemsList';
import NewFileDownload from './components/NewFileDownload';

function App() {
  const [isDarkMode, setDarkMode] = useState(true);
  const [dummy1, setDummy1] = useState("")
  const [dummy2, setDummy2] = useState("")
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [versionMessage, setVersionMessage] = useState('');

  const toggleDarkMode = () => {
    setDarkMode(!isDarkMode);
  };

  const openModal = () => {
    setIsModalOpen(true);
  };

  const closeModal = () => {
    setIsModalOpen(false);
  };

  const getLatestVersion = async () => {
    const url = 'https://raw.githubusercontent.com/gudarzi/SaveHere/refs/heads/v2.0/backend/wwwroot/version.json';

    try {
      const response = await fetch(url);

      if (response.ok) {
        const data = await response.json();
        const latestVersion = data.current_version;
        checkForUpdate(latestVersion);
      }
      else {
        setVersionMessage('Error fetching the latest version.');
        openModal();
      }
    }
    catch (error: unknown) {
      if (error instanceof Error) {
        setVersionMessage(`Error fetching the latest version: ${error.message}`);
      }
      else {
        setVersionMessage('An unknown error occurred while fetching the latest version.');
      }

      openModal();
    }
  };

  const getLocalVersion = async (): Promise<string | null> => {
    try {
      const response = await fetch('/version.json');

      if (response.ok) {
        const data = await response.json();
        return data.current_version;
      }
      else {
        throw new Error('Local version file not found.');
      }
    }
    catch (error: unknown) {
      if (error instanceof Error) {
        setVersionMessage(`Error fetching local version: ${error.message}`);
      }
      else {
        setVersionMessage('An unknown error occurred while fetching the local version.');
      }

      openModal();
      return null;
    }
  };

  const checkForUpdate = async (latestVersion: string) => {
    const currentVersion = await getLocalVersion();

    if (latestVersion !== currentVersion) {
      setVersionMessage('A new version is available! Go get it now at: https://github.com/gudarzi/SaveHere');
    }
    else {
      setVersionMessage('You are using the latest version.');
    }

    openModal();
  };

  return (
    <div className={`App ${isDarkMode ? 'dark' : ''}`}>
      <nav className="text-white p-1">
        <div className="container mx-auto flex justify-between items-center">

          <div className="container mx-auto flex justify-left items-center">
            <img src="/icon.svg" alt="Icon" />
            <svg width="200" height="50" xmlns="http://www.w3.org/2000/svg">
              <defs>
                <linearGradient id="textGradient" x1="0%" y1="25%" x2="100%" y2="75%">
                  <stop offset="0%" stopColor="red" />
                  <stop offset="100%" stopColor="#5555FFFF" />
                </linearGradient>
              </defs>
              <text x="10" y="35" fontFamily="Arial" fontSize="30" fill="url(#textGradient)">SaveHere</text>
              <rect x="10" y="38" width="140" height="2" fill="url(#textGradient)" />
            </svg>
          </div>

          <button className="p-1 rounded-full hover:bg-[#FFFFFF33] ml-2" onClick={getLatestVersion}>
            <img src="/update-icon.svg" alt="update-icon" />
          </button>
          <a href='https://github.com/gudarzi/SaveHere' target='_blank' className='p-1 ml-2 rounded-full hover:bg-[#FFFFFF33]'>
            <img src="/github-icon.svg" alt="github-icon" />
          </a>
          <button className="p-1 rounded-full hover:bg-[#FFFFFF33] ml-2" onClick={toggleDarkMode}>
            {isDarkMode ?
              <img src="/moon-icon.svg" alt="moon-icon" />
              :
              <img src="/sun-icon.svg" alt="sun-icon" />
            }
          </button>
        </div>
      </nav>

      <NewFileDownload onNewFileAdded={() => setDummy1(dummy1 + 1)} />

      <QueueItemsList dummy={dummy1} onDownloadFinished={() => setDummy2(dummy2 + 1)} />

      <DownloadedFilesList dummy={dummy2} />

      {isModalOpen && (
        <div className="fixed inset-0 bg-black bg-opacity-75 flex items-center justify-center">
          <div className="bg-white p-5 rounded-lg max-w-md w-full shadow-md">
            <h2>Check for Updates</h2>
            <div>{versionMessage}</div>
            <button className='mt-5 px-5 py-2.5 bg-blue-600 text-white border-none rounded cursor-pointer hover:bg-blue-700' onClick={closeModal}>Close</button>
          </div>
        </div>
      )}

    </div>
  );
}

export default App