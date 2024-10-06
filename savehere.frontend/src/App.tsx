import {useState} from "react";
import "./App.css";
import DownloadedFilesList from "./components/DownloadedFilesList";
import QueueItemsList from "./components/QueueItemsList";
import NewFileDownload from "./components/NewFileDownload";
import HeaderRequestContainer from "./components/HeaderRequest/HeaderRequestContainer";

function App() {
  const [isDarkMode, setDarkMode] = useState(true);
  const [dummy1, setDummy1] = useState("");
  const [dummy2, setDummy2] = useState("");
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [versionMessage, setVersionMessage] = useState("");
  const [headerList, setHeaderList] = useState<[]>([]);

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
    const url =
      "https://raw.githubusercontent.com/gudarzi/SaveHere/master/frontend/version.json";

    try {
      const response = await fetch(url);

      if (response.ok) {
        const data = await response.json();
        const latestVersion = data.current_version;
        checkForUpdate(latestVersion);
      } else {
        setVersionMessage("Error fetching the latest version.");
        openModal();
      }
    } catch (error: unknown) {
      if (error instanceof Error) {
        setVersionMessage(
          `Error fetching the latest version: ${error.message}`
        );
      } else {
        setVersionMessage(
          "An unknown error occurred while fetching the latest version."
        );
      }

      openModal();
    }
  };

  const getLocalVersion = async (): Promise<string | null> => {
    try {
      const response = await fetch("/version.json");

      if (response.ok) {
        const data = await response.json();
        return data.current_version;
      } else {
        throw new Error("Local version file not found.");
      }
    } catch (error: unknown) {
      if (error instanceof Error) {
        setVersionMessage(`Error fetching local version: ${error.message}`);
      } else {
        setVersionMessage(
          "An unknown error occurred while fetching the local version."
        );
      }

      openModal();
      return null;
    }
  };

  const checkForUpdate = async (latestVersion: string) => {
    const currentVersion = await getLocalVersion();

    if (latestVersion !== currentVersion) {
      setVersionMessage(
        "A new version is available! Go get it now at: https://github.com/gudarzi/SaveHere"
      );
    } else {
      setVersionMessage("You are using the latest version.");
    }

    openModal();
  };

  return (
    <div className={`App ${isDarkMode ? "dark" : ""}`}>
      <nav className="text-white p-1">
        <div className="container mx-auto flex justify-between items-center">
          <div className="container mx-auto flex justify-left items-center">
            <img src="/icon.svg" alt="Icon" />
            <svg width="200" height="50" xmlns="http://www.w3.org/2000/svg">
              <defs>
                <linearGradient
                  id="textGradient"
                  x1="0%"
                  y1="25%"
                  x2="100%"
                  y2="75%"
                >
                  <stop offset="0%" stopColor="red" />
                  <stop offset="100%" stopColor="#5555FFFF" />
                </linearGradient>
              </defs>
              <text
                x="10"
                y="35"
                fontFamily="Arial"
                fontSize="30"
                fill="url(#textGradient)"
              >
                SaveHere
              </text>
              <rect
                x="10"
                y="38"
                width="140"
                height="2"
                fill="url(#textGradient)"
              />
            </svg>

            <a
              href="/filemanager/"
              target="_blank"
              className="p-2 m-1 rounded-xl hover:bg-gray-500 bg-gray-700"
            >
              File Manager
            </a>
            <a
              href="/swagger/index.html"
              target="_blank"
              className="p-2 m-1 rounded-xl hover:bg-gray-500 bg-gray-700"
            >
              Swagger
            </a>
          </div>

          <a
            href="https://github.com/gudarzi/SaveHere"
            target="_blank"
            className="p-2 m-1"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="w-6 h-6 rounded-xl hover:bg-gray-500"
              viewBox="0 0 24 24"
              fill="white"
            >
              <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z" />
            </svg>
          </a>
          <button
            className="p-1 rounded-full hover:bg-[#FFFFFF33]"
            onClick={toggleDarkMode}
          >
            {isDarkMode ? (
              <svg
                xmlns="http://www.w3.org/2000/svg"
                viewBox="0 0 24 24"
                fill="white"
                className="w-6 h-6"
              >
                <path
                  fillRule="evenodd"
                  d="M9.528 1.718a.75.75 0 0 1 .162.819A8.97 8.97 0 0 0 9 6a9 9 0 0 0 9 9 8.97 8.97 0 0 0 3.463-.69.75.75 0 0 1 .981.98 10.503 10.503 0 0 1-9.694 6.46c-5.799 0-10.5-4.7-10.5-10.5 0-4.368 2.667-8.112 6.46-9.694a.75.75 0 0 1 .818.162Z"
                  clipRule="evenodd"
                />
              </svg>
            ) : (
              <svg
                xmlns="http://www.w3.org/2000/svg"
                viewBox="0 0 24 24"
                fill="white"
                className="w-6 h-6"
              >
                <path d="M12 2.25a.75.75 0 0 1 .75.75v2.25a.75.75 0 0 1-1.5 0V3a.75.75 0 0 1 .75-.75ZM7.5 12a4.5 4.5 0 1 1 9 0 4.5 4.5 0 0 1-9 0ZM18.894 6.166a.75.75 0 0 0-1.06-1.06l-1.591 1.59a.75.75 0 1 0 1.06 1.061l1.591-1.59ZM21.75 12a.75.75 0 0 1-.75.75h-2.25a.75.75 0 0 1 0-1.5H21a.75.75 0 0 1 .75.75ZM17.834 18.894a.75.75 0 0 0 1.06-1.06l-1.59-1.591a.75.75 0 1 0-1.061 1.06l1.59 1.591ZM12 18a.75.75 0 0 1 .75.75V21a.75.75 0 0 1-1.5 0v-2.25A.75.75 0 0 1 12 18ZM7.758 17.303a.75.75 0 0 0-1.061-1.06l-1.591 1.59a.75.75 0 0 0 1.06 1.061l1.591-1.59ZM6 12a.75.75 0 0 1-.75.75H3a.75.75 0 0 1 0-1.5h2.25A.75.75 0 0 1 6 12ZM6.697 7.757a.75.75 0 0 0 1.06-1.06l-1.59-1.591a.75.75 0 0 0-1.061 1.06l1.59 1.591Z" />
              </svg>
            )}
          </button>
          <button
            className="p-1 rounded-full hover:bg-[#FFFFFF33] ml-2"
            onClick={getLatestVersion}
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 24 24"
              fill="white"
              className="w-6 h-6"
            >
              <path d="M12 2a10 10 0 100 20 10 10 0 000-20zm0 18a8 8 0 110-16 8 8 0 010 16z" />
              <path d="M12 11.293l4.146-4.147.708.708L12 12.707l-4.854-4.853.708-.708L12 11.293z" />
            </svg>
          </button>
        </div>
      </nav>

      <HeaderRequestContainer
        headerList={headerList}
        setHeaderList={setHeaderList}
      />

      <NewFileDownload onNewFileAdded={() => setDummy1(dummy1 + 1)} />

      <QueueItemsList
        dummy={dummy1}
        onDownloadFinished={() => setDummy2(dummy2 + 1)}
        requestHeaders={headerList}
      />

      <DownloadedFilesList dummy={dummy2} />

      {isModalOpen && (
        <div className="modal-overlay">
          <div className="modal">
            <h2>Check for Updates</h2>
            <div>{versionMessage}</div>
            <button onClick={closeModal}>Close</button>
          </div>
        </div>
      )}
    </div>
  );
}

export default App;
