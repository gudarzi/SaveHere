<div align="center">
  <h1>SaveHere</h1>
  <h4>[WIP] Minimal Cloud File Manager</h4>
  <img src="https://github.com/gudarzi/SaveHere/assets/30085894/ea533811-32b3-4e32-be22-0b6b85005df5" alt="Screen Shot of SaveHere App">
</div>

## Table of Contents

- [What this app does](#what-this-app-does)
- [Dependencies](#dependencies)
- [How to run this app](#how-to-run-this-app)
- [To Do](#to-do)
- [How to contribute](#how-to-contribute)
- [Disclaimer](#disclaimer)

## What this app does

SaveHere is a minimal cloud file manager that allows you to download files from the internet and store them on your own server, either locally or on a VPS. It uses Docker, React for frontend, ASP.NET Core for backend, Nginx for serving downloaded files and a few Docker images for other functionalities.

The app was built to address the issue of downloading large files from slow servers or unstable connections. With SaveHere, you can enter the URL of the file you want to download, and the app will download it to your server. You can then download the file from your own server using your own domain name and URL, with the ability to pause and resume the download as needed.

SaveHere keeps a list of all download requests and downloaded files, allowing you to easily manage your files. You can delete files from the server at any time. The app currently does not support uploading files from your local machine, but that feature is planned for a future release.

SaveHere is designed to be lightweight and easy to use, with a focus on simplicity and functionality. Whether you're downloading files for personal or professional use, SaveHere can help you do it more efficiently and reliably.


## Dependencies

To run SaveHere, you will need to have the following dependencies installed:

* [Docker](https://docs.docker.com/get-docker/): SaveHere is containerized using Docker, so you will need to have Docker installed on your machine.
* [Docker Compose](https://docs.docker.com/compose/install/): SaveHere uses Docker Compose to manage its containers, so you will need to have Docker Compose installed as well.
* [Node.js and npm](https://github.com/nvm-sh/nvm): SaveHere's frontend is built using React, which requires Node.js and npm to be installed. I recommend using nvm (Node Version Manager) to manage your Node.js installations.
* (Optional) [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) and [vscode](https://code.visualstudio.com/) for development.

In addition, it is recommended that you run SaveHere behind a reverse proxy such as [Nginx](https://nginx.org/) or [Nginx Proxy Manager](https://nginxproxymanager.com/). This will allow you to access the app using your own domain name and SSL certificate, and provide additional security and performance benefits.


## How to run this app

To run SaveHere, follow these steps:

1. Clone the repository from GitHub and navigate into the directory:
```bash
git clone https://github.com/gudarzi/SaveHere.git
cd SaveHere
```

2. Create the `db` directory and an empty `database.sqlite3.db` file:
```bash
mkdir db
touch db/database.sqlite3.db
```

3. Run the containers using Docker Compose in detached mode:
```bash
docker compose -f docker-compose.production.yml up -d
```

4. Build the frontend app
```bash
cd savehere.frontend
npm i
npm run build
```

5. The app is now available at the address `http://172.17.0.1:18480`.

6. (Optional) If you encounter permission issues with the downloads folder, change the owner of the folder and all of its content to 1000:1000 and set the permissions to 777:
```bash
sudo chown -R 1000:1000 downloads/
sudo chmod -R 777 downloads/
```


## To Do
- Fix the packaging of the app and create 1 single docker image
- Add an option for using filenames that the target server sends (instead of using the url)
- Show more data and info about the file before downloading it
- Add reporting download status to the user (useful for big files)
- Add user accounts and set their access policies
- Check [issues](https://github.com/gudarzi/SaveHere/issues) for more!


## How to contribute

I welcome contributions from the community to help improve SaveHere. If you're interested in contributing, please check the [To Do](#to-do) list or take a look at the project's [issues](https://github.com/gudarzi/SaveHere/issues) page to see if there are any open issues that you can help with. You can also submit pull requests with bug fixes, new features, or other improvements. Before submitting a pull request, please make sure that your code follows the project's coding standards and that all tests are passing. I will review all pull requests as soon as possible and provide feedback if necessary.


## Disclaimer

This is a hobby project that I work on in my spare time. While I try to make it as good as possible, I cannot guarantee that it is free of bugs or errors, or that it will meet your specific needs. As such, I cannot be held responsible for any damage or loss that may result from using this project.

I welcome any contributions that can help improve the project, but I cannot guarantee that I will be able to incorporate all suggested changes or respond to all feedback. I also reserve the right to reject any contributions that I deem inappropriate or not in line with the project's goals.

