import { Box } from "@mui/material";
import Modal from "@mui/material/Modal";
import { useState } from "react";
import { videoFilesExtensions } from "./videoFilesExtensions";

export const PreviewVideoFile = (props: { videoUrl: string }) => {
    console.log(props.videoUrl);
    const [showPreviewVideoModal, setshowPreviewVideoModal] = useState(false)

    const isVideoFile = (url: string) => {
        for (let i = 0; i < videoFilesExtensions.length; i++) {
            if (url.toLowerCase().endsWith(videoFilesExtensions[i])) {
                return true;
            }
        }

        return false;
    }

    const previewVideoFile = () => {
        setshowPreviewVideoModal(true);
        setTimeout(() => { // Ensure DOM is updated before adding the video
            const videoContainer = document.getElementById("videoPlayerContainer");
            if (videoContainer && !document.getElementById("videoPlayer")) {
                const video = document.createElement("video");
                video.setAttribute("id", "videoPlayer");
                video.setAttribute("controls", "true");
                video.setAttribute("autoPlay", "true");

                const source = document.createElement("source");
                source.setAttribute("src", props.videoUrl);
                source.setAttribute("type", "video/mp4");

                video.appendChild(source);
                videoContainer.appendChild(video);
            }
        }, 0);
    }

    const closeVideoPlayer = () => {
        const videoContainer = document.getElementById("videoPlayerContainer");
        const video = document.getElementById("videoPlayer") as HTMLVideoElement;
        if (video && videoContainer) {
            video.pause(); // Pause the video
            videoContainer.removeChild(video); // Remove the video element
        }
    };

    const style = {
        position: 'absolute' as 'absolute',
        top: '50%',
        left: '50%',
        transform: 'translate(-50%, -50%)',
        width: '50%',
        bgcolor: 'background.paper',
        border: '2px solid #000',
        boxShadow: 24,
        p: 4,
    };

    const closeModal = (): void => {
        closeVideoPlayer();

        setshowPreviewVideoModal(false);
    }

    return (
        <>
            <Modal
                open={showPreviewVideoModal}
                onClose={closeModal}
                aria-labelledby="modal-modal-title"
                aria-describedby="modal-modal-description"
            >
                <Box sx={style}>
                    <div id="videoPlayerContainer" className="w-3/4 h-3/4 mx-auto my-auto"> </div>
                </Box>
            </Modal>

            <button onClick={() => previewVideoFile()} className="p-1 rounded-full hover:bg-[#FFFFFF33]" disabled={!isVideoFile(props.videoUrl)}>
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M15 12A3 3 0 1112 9a3 3 0 013 3zm7.242-1.757a12 12 0 00-2.868-3.69A10 10 0 0012 4a10 10 0 00-7.374 3.19 12 12 0 00-2.868 3.69 1.25 1.25 0 000 1.515 12 12 0 002.868 3.69A10 10 0 0012 20a10 10 0 007.374-3.19 12 12 0 002.868-3.69 1.25 1.25 0 000-1.515z" />
                </svg>
            </button>
        </>

    );

};

