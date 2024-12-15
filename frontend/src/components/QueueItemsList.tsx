import { useState, useEffect, useRef } from 'react';
import { PreviewVideoFile } from './VideoFilePreview';
import Modal from '@mui/material/Modal';
import { Alert, Box, Button, CircularProgress, TextField, Typography } from '@mui/material';

interface QueueItem {
    id: number
    inputUrl: string
    status: 0 | 1 | 2 | 3
    progressPercentage: number
    downloadSpeedInBytesPerSecond: number
    downloadSpeedInBytesPerSecondAvg: number
}

const statusMapping = {
    0: 'Paused',
    1: 'Downloading',
    2: 'Finished',
    3: 'Cancelled',
}

const style = {
    position: 'absolute' as 'absolute',
    top: '50%',
    left: '50%',
    transform: 'translate(-50%, -50%)',
    width: 'auto',
    bgcolor: 'background.paper',
    border: '2px solid #000',
    boxShadow: 24,
    p: 4,
};

const QueueItemsList = (props: { dummy: string, onDownloadFinished: () => unknown }) => {
    const [data, setData] = useState<QueueItem[]>([])
    const [useHeadersForFilename, setUseHeadersForFilename] = useState(1)
    const socketRef = useRef<WebSocket | null>(null)
    const [showProxyDownloadModal, setShowProxyDownloadModal] = useState(false)
    const [proxyServer, setProxyServer] = useState({ host: '', port: 0, protocol: '' });
    const [pageErrors, setPageErrors] = useState<string[]>([]);
    const [pageInfo, setPageInfo] = useState<string[]>([]);

    const [loading, setLoading] = useState(false);

    const [fileId, setFileId] = useState(0);

    useEffect(() => {
        const wsUrl = "/ws"
        socketRef.current = new WebSocket(wsUrl)

        socketRef.current.addEventListener('open', () => {
            console.log('Connected to the WebSocket server')
        })

        socketRef.current.addEventListener('message', (event) => {
            console.log('Message from server:', event.data)
            if (event.data.startsWith('progress:')) {
                const [, id, progress] = event.data.split(':')
                setData(prevData =>
                    prevData.map(item =>
                        item.id === Number(id) ? { ...item, status: 1, progressPercentage: Number(progress) } : item
                    )
                )
                // This helps provide accurate end-of-download information even if a refresh of the page occured
                if (Number(progress) === 100) {
                    setData(prevData =>
                        prevData.map(item =>
                            item.id === Number(id) ? { ...item, status: 2, progressPercentage: 100, downloadSpeedInBytesPerSecond: 0 } : item
                        )
                    )
                }
            }
            else if (event.data.startsWith('speed:')) {
                const [, id, speed] = event.data.split(':')
                setData(prevData =>
                    prevData.map(item =>
                        item.id === Number(id) ? { ...item, downloadSpeedInBytesPerSecond: Number(speed) } : item
                    )
                )
            }
            else if (event.data.startsWith('speedavg:')) {
                const [, id, speed] = event.data.split(':')
                setData(prevData =>
                    prevData.map(item =>
                        item.id === Number(id) ? { ...item, downloadSpeedInBytesPerSecondAvg: Number(speed) } : item
                    )
                )
            }
        })

        socketRef.current.addEventListener('close', () => {
            console.log('Disconnected from the WebSocket server')
        })

        socketRef.current.addEventListener('error', (error) => {
            console.error('WebSocket error:', error)
        })

        // Cleanup on component unmount
        return () => {
            if (socketRef.current) {
                socketRef.current.close();
            }
        };
    }, [props.dummy])

    useEffect(() => {
        fetchList()
    }, [props.dummy])

    useEffect(() => {
        fetchList()
    }, [])

    useEffect(() => {
        const getFormattedDateTime = () => {
            const now = new Date();
            return now.toLocaleString();
        };

        const interval = setInterval(() => {
            if (socketRef.current && socketRef.current.readyState === WebSocket.OPEN) {
                console.log(`keep-alive: ${getFormattedDateTime()}`);
                socketRef.current.send(JSON.stringify({ type: 'keep-alive' }));
            }
        }, 50000); // Send a keep-alive message every 50 seconds

        return () => clearInterval(interval);
    }, [])

    const convertSize = (sizeBytes: number | undefined) => {
        if (sizeBytes === undefined) return "0 B/s";
        if (sizeBytes === 0) return "0 B/s";
        const sizeName = ["B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
        const i = Math.floor(Math.log(sizeBytes) / Math.log(1024));
        const p = Math.pow(1024, i);
        const s = Math.round(sizeBytes / p * 100) / 100; // Keep two decimal places
        return `${s} ${sizeName[i]}/s`;
    };


    const fetchList = async () => {
        try {
            const res = await fetch('/api/FileDownloadQueueItems')
            const result = await res.json()
            setData(result)
        } catch (err) {
            setData([])
            console.error(err)
        }
    }

    const requestDownload = async (id: number) => {
        setLoading(true);
        try {
            console.log(proxyServer);
            const response = await fetch(`api/FileDownloadQueueItems/startdownload`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    id,
                    useHeadersForFilename: Boolean(useHeadersForFilename),
                    proxyServer: proxyServer,
                }),
            })

            if (response.ok) {
                setLoading(false);
                closeModal();
                props.onDownloadFinished()
            } else {
                setLoading(false);
                console.error('Failed to download item:', response.statusText)
                setPageErrors(["Failed to download item:" + response.statusText]);
            }
        } catch (error) {
            setLoading(false);
            console.error('Error:', error)
        }
        finally {
            setLoading(false);
            fetchList()
        }
    }

    const cancelDownload = async (id: number) => {
        try {
            const response = await fetch(`api/FileDownloadQueueItems/canceldownload`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    id
                }),
            });

            if (response.ok) {
                props.onDownloadFinished()
            } else {
                console.error('Failed to cancel item:', response.statusText)
            }
        } catch (error) {
            console.error('Error:', error)
        }
        finally {
            fetchList()
        }
    }

    const handleRequestProxyDownload = async (nodeId: number) => {
        console.log('Requesting proxy download for:', nodeId);
        setShowProxyDownloadModal(true);
        setFileId(nodeId);
    }

    const closeModal = (): void => {
        setShowProxyDownloadModal(false);
        setPageErrors([]);
        setPageInfo([]);
    }

    const onChangeProxyServerProtocol = (event: React.ChangeEvent<HTMLInputElement>) => {
        setProxyServer({ ...proxyServer, protocol: event.target.value });
    }

    const onChangeProxyServerUrl = (event: React.ChangeEvent<HTMLInputElement>) => {
        setProxyServer({ ...proxyServer, host: event.target.value });
    }

    const onChangeProxyServerPort = (event: React.ChangeEvent<HTMLInputElement>) => {
        setProxyServer({ ...proxyServer, port: Number(event.target.value) });
    }

    const testConnection = async () => {
        setPageErrors([]);
        setPageInfo([]);
        try {
            setLoading(true);
            const response = await fetch(`api/FileDownloadQueueItems/testconnection`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(
                    proxyServer
                ),
            });

            if (response.ok) {
                setLoading(false);
                console.log('Proxy server is reachable');
                setPageInfo(['Proxy server is reachable']);
            } else {
                setLoading(false);
                console.error('Failed to test proxy server:', response.statusText);

                setPageErrors(["Failed to connect to proxy:" + response.statusText]);

            }
        } catch (error) {
            console.error('Error:', error);
        }
    }

    const renderNode = (node: QueueItem) => {
        const handleDelete = async () => {
            try {
                const response = await fetch(`api/FileDownloadQueueItems/${node.id}`, {
                    method: 'DELETE',
                });

                if (!response.ok) {
                    console.error('Failed to delete item:', response.statusText)
                }
            } catch (error) {
                console.error('Error:', error)
            }
            finally {
                fetchList()
            }
        }

        return (
            <>
                <li key={node.id} className="container flex items-center justify-normal w-full p-1 text-sm font-medium text-left text-gray-800 rounded-lg focus:outline-none dark:text-white hover:bg-gray-100 dark:hover:bg-gray-700 flex-wrap">

                    <div className="ml-1 p-1 rounded-xl bg-gray-400 dark:bg-gray-700">{statusMapping[node.status]}</div>
                    <div className="ml-1 p-1 rounded-xl bg-gray-400 dark:bg-gray-700">{node.progressPercentage}%</div>
                    <div className="ml-1 p-1 rounded-xl whitespace-nowrap bg-gray-400 dark:bg-gray-700">{convertSize(node.downloadSpeedInBytesPerSecond) ?? "KB/s"}</div>
                    <div className="ml-1 p-1 rounded-xl whitespace-nowrap bg-gray-400 dark:bg-gray-700">{convertSize(node.downloadSpeedInBytesPerSecondAvg) ?? "KB/s"}</div>
                    <select onChange={(e) => setUseHeadersForFilename(Number(e.target.value))} className="ml-1 p-1 rounded-xl bg-gray-400 dark:bg-gray-700 text-gray-800 dark:text-white hover:bg-[#FFFFFF33]">
                        <option value="1" className="dark:bg-gray-500">Use Headers For Filename</option>
                        <option value="0" className="dark:bg-gray-500">Use Url For Filename</option>
                    </select>
                    <div className="ml-1 px-2 rounded-xl whitespace-nowrap bg-gray-400 dark:bg-gray-700">
                        <button onClick={handleDelete} className="p-1 rounded-full hover:bg-[#FFFFFF33]">
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5">
                                <path fillRule="evenodd" d="M16.5 4.478v.227a48.816 48.816 0 0 1 3.878.512.75.75 0 1 1-.256 1.478l-.209-.035-1.005 13.07a3 3 0 0 1-2.991 2.77H8.084a3 3 0 0 1-2.991-2.77L4.087 6.66l-.209.035a.75.75 0 0 1-.256-1.478A48.567 48.567 0 0 1 7.5 4.705v-.227c0-1.564 1.213-2.9 2.816-2.951a52.662 52.662 0 0 1 3.369 0c1.603.051 2.815 1.387 2.815 2.951Zm-6.136-1.452a51.196 51.196 0 0 1 3.273 0C14.39 3.05 15 3.684 15 4.478v.113a49.488 49.488 0 0 0-6 0v-.113c0-.794.609-1.428 1.364-1.452Zm-.355 5.945a.75.75 0 1 0-1.5.058l.347 9a.75.75 0 1 0 1.499-.058l-.346-9Zm5.48.058a.75.75 0 1 0-1.498-.058l-.347 9a.75.75 0 0 0 1.5.058l.345-9Z" clipRule="evenodd" />
                            </svg>
                        </button>
                        <button onClick={() => cancelDownload(node.id)} className="p-1 rounded-full hover:bg-[#FFFFFF33]">
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5">
                                <path fillRule="evenodd" d="M12 2.25c-5.385 0-9.75 4.365-9.75 9.75s4.365 9.75 9.75 9.75 9.75-4.365 9.75-9.75S17.385 2.25 12 2.25Zm-1.72 6.97a.75.75 0 1 0-1.06 1.06L10.94 12l-1.72 1.72a.75.75 0 1 0 1.06 1.06L12 13.06l1.72 1.72a.75.75 0 1 0 1.06-1.06L13.06 12l1.72-1.72a.75.75 0 1 0-1.06-1.06L12 10.94l-1.72-1.72Z" clipRule="evenodd" />
                            </svg>
                        </button>
                        <button onClick={() => requestDownload(node.id)} className="p-1 rounded-full hover:bg-[#FFFFFF33]">
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5">
                                <path d="M12 1.5a.75.75 0 0 1 .75.75V7.5h-1.5V2.25A.75.75 0 0 1 12 1.5ZM11.25 7.5v5.69l-1.72-1.72a.75.75 0 0 0-1.06 1.06l3 3a.75.75 0 0 0 1.06 0l3-3a.75.75 0 1 0-1.06-1.06l-1.72 1.72V7.5h3.75a3 3 0 0 1 3 3v9a3 3 0 0 1-3 3h-9a3 3 0 0 1-3-3v-9a3 3 0 0 1 3-3h3.75Z" />
                            </svg>
                        </button>
                        <button onClick={() => handleRequestProxyDownload(node.id)} className="p-1 rounded-full hover:bg-[#FFFFFF33]">
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5">
                                <path d="M12 1.5a.75.75 0 0 1 .75.75V7.5h-1.5V2.25A.75.75 0 0 1 12 1.5ZM11.25 7.5v5.69l-1.72-1.72a.75.75 0 0 0-1.06 1.06l3 3a.75.75 0 0 0 1.06 0l3-3a.75.75 0 1 0-1.06-1.06l-1.72 1.72V7.5h3.75a3 3 0 0 1 3 3v9a3 3 0 0 1-3 3h-9a3 3 0 0 1-3-3v-9a3 3 0 0 1 3-3h3.75Z" />
                            </svg>
                        </button>

                        <PreviewVideoFile videoUrl={node.inputUrl} />

                    </div>

                        {/* <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5 m-1 dark:text-slate-300 inline-block">
                            <path d="M5.625 1.5c-1.036 0-1.875.84-1.875 1.875v17.25c0 1.035.84 1.875 1.875 1.875h12.75c1.035 0 1.875-.84 1.875-1.875V12.75A3.75 3.75 0 0 0 16.5 9h-1.875a1.875 1.875 0 0 1-1.875-1.875V5.25A3.75 3.75 0 0 0 9 1.5H5.625Z" />
                            <path d="M12.971 1.816A5.23 5.23 0 0 1 14.25 5.25v1.875c0 .207.168.375.375.375H16.5a5.23 5.23 0 0 1 3.434 1.279 9.768 9.768 0 0 0-6.963-6.963Z" />
                        </svg> */}

                        <div className="ml-2 flex-grow min-w-0 overflow-x-auto text-wrap break-words">
                            {node.inputUrl}
                        </div>

                </li>
            </>
        );
    };

    return (
        <div className="p-3 bg-slate-100 dark:bg-gray-600 rounded-md w-4/5 mx-auto my-1">

            <Modal
                open={showProxyDownloadModal}
                onClose={closeModal}
                aria-labelledby="modal-modal-title"
                aria-describedby="modal-modal-description"
            >
                <Box sx={style}>
                    {loading ? <CircularProgress /> :
                        <>
                            <Typography id="modal-modal-title" variant="h6" component="h2">
                                Use proxy server to download this file
                            </Typography>

                            {pageInfo.map(info => {
                                return <Alert severity="success">{info}</Alert>
                            })}

                            {pageErrors.map(error => {
                                return <Alert severity="error">{error}</Alert>
                            })}

                            <Typography id="modal-modal-description" sx={{ mt: 2 }}>

                                <TextField id="outlined-basic" label="proxy server protocol" variant="outlined" onChange={onChangeProxyServerProtocol} value={proxyServer.protocol} />
                                <TextField id="outlined-basic" label="proxy server url" variant="outlined" onChange={onChangeProxyServerUrl} value={proxyServer.host} />
                                <TextField type='number' id="outlined-basic" label="proxy server port" variant="outlined" onChange={onChangeProxyServerPort} value={proxyServer.port} />

                            </Typography>

                            <Typography id="modal-modal-description" sx={{ mt: 2 }}>

                                <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
                                    <Button style={{ marginRight: "10px" }} variant="contained" onClick={() => requestDownload(fileId)} >Download</Button>

                                    <Button style={{ marginRight: "10px" }} variant="contained" onClick={testConnection} >test connection</Button>

                                    <Button variant="text" onClick={closeModal}>cancel</Button>
                                </div>
                            </Typography>
                        </>
                    }
                </Box>
            </Modal>
            <div className='flex flex-row'>
                <h3 className="font-bold text-lg ml-2 dark:text-slate-100">
                    Download Queue
                </h3>
                <button className="ml-2" onClick={fetchList}>
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-6 h-6 p-1 rounded-full hover:bg-[#FFFFFF33] dark:text-slate-100">
                        <path fillRule="evenodd" d="M4.755 10.059a7.5 7.5 0 0 1 12.548-3.364l1.903 1.903h-3.183a.75.75 0 1 0 0 1.5h4.992a.75.75 0 0 0 .75-.75V4.356a.75.75 0 0 0-1.5 0v3.18l-1.9-1.9A9 9 0 0 0 3.306 9.67a.75.75 0 1 0 1.45.388Zm15.408 3.352a.75.75 0 0 0-.919.53 7.5 7.5 0 0 1-12.548 3.364l-1.902-1.903h3.183a.75.75 0 0 0 0-1.5H2.984a.75.75 0 0 0-.75.75v4.992a.75.75 0 0 0 1.5 0v-3.18l1.9 1.9a9 9 0 0 0 15.059-4.035.75.75 0 0 0-.53-.918Z" clipRule="evenodd" />
                    </svg>
                </button>
            </div>
            <ul className="bg-[#11111122] dark:bg-[#FFFFFF22] p-3 text-sm leading-6 rounded-md">
                {data && data.length > 0 ? (
                    data.map((node) => <div key={node.id}>{renderNode(node)}</div>)
                ) : (
                    <li className="text-center font-bold">Nothing to Show!</li>
                )}
            </ul>
        </div>
    );
}

export default QueueItemsList