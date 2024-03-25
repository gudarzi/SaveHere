import { useState, useEffect } from 'react';

interface FileSystemInfo {
    Name: string;
    FullName: string;
    Type: 'file' | 'directory';
    Children?: FileSystemInfo[];
    Length?: number;
    Extension?: string;
}

const DownloadedFilesList = () => {
    const [data, setData] = useState<FileSystemInfo[]>([]);

    useEffect(() => {
        fetch('/api/file/list')
            .then((res) => res.json())
            .then((result) => {
                setData(result);
            })
            .catch((err) => {
                console.error(err);
            });
    }, []);

    const renderNode = (node: FileSystemInfo, level: number) => {
        if (node.Type === 'directory') {
            return (
                <li key={node.FullName}>
                    <FileNode
                        node={node}
                        level={level}
                        renderNode={renderNode}
                    />
                </li>
            );
        } else {
            return (
                <li key={node.FullName} className="py-1">
                    {node.Name} - {node.Length} bytes, {node.Extension}
                </li>
            );
        }
    };

    return (
        <div className="p-12 bg-white-800 dark:bg-gray-800">
            <h5 className="text-slate-900 font-semibold mb-4 text-sm leading-6 dark:text-slate-100">
                Downloaded Files
            </h5>
            <ul className="text-slate-700 text-sm leading-6">
                {data.map((node) => renderNode(node, 1))}
            </ul>
        </div>
    );
};

const FileNode = ({
    node,
    level,
    renderNode,
}: {
    node: FileSystemInfo;
    level: number;
    renderNode: (node: FileSystemInfo, level: number) => JSX.Element;
}) => {
    const [open, setOpen] = useState(false);

    const handleClick = () => {
        setOpen(!open);
    };

    return (
        <>
            <button
                className="flex items-center w-full p-2 text-sm font-medium text-left text-gray-800 rounded-lg focus:outline-none dark:text-white hover:bg-gray-100 dark:hover:bg-gray-700"
                onClick={handleClick}
            >
                <svg
                    className={`w-5 h-5 mr-3 ${!open ? 'transform -rotate-90' : ''
                        } transition-transform duration-300`}
                    xmlns="http://www.w3.org/2000/svg"
                    viewBox="0 0 20 20"
                    fill="currentColor"
                >
                    <path
                        fillRule="evenodd"
                        d="M5.293 7.293a1 1 0 01 1.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z"
                        clipRule="evenodd"
                    />
                </svg>
                {node.Name}
            </button>
            {open && (
                <ul className="ml-4 mb-2">
                    {node.Children?.map((child) =>
                        renderNode(child, level + 1)
                    )}
                </ul>
            )}
        </>
    );
};

export default DownloadedFilesList;