package com.darkiiuseai.itunesrpc;

import javafx.scene.image.Image;

import java.util.Objects;

/**
 * Classe représentant les informations d'une piste musicale
 */
public class TrackInfo {
    private final String title;
    private final String artist;
    private final String album;
    private final String duration;
    private final int trackNumber;
    private final int totalTracks;
    private final Image albumArt;
    
    /**
     * Constructeur pour créer un nouvel objet TrackInfo
     * 
     * @param title Le titre de la piste
     * @param artist L'artiste de la piste
     * @param album L'album de la piste
     * @param duration La durée de la piste
     * @param trackNumber Le numéro de la piste dans l'album/playlist
     * @param totalTracks Le nombre total de pistes dans l'album/playlist
     * @param albumArt L'image de la pochette d'album
     */
    public TrackInfo(String title, String artist, String album, String duration, 
                    int trackNumber, int totalTracks, Image albumArt) {
        this.title = title;
        this.artist = artist;
        this.album = album;
        this.duration = duration;
        this.trackNumber = trackNumber;
        this.totalTracks = totalTracks;
        this.albumArt = albumArt;
    }
    
    public String getTitle() {
        return title;
    }
    
    public String getArtist() {
        return artist;
    }
    
    public String getAlbum() {
        return album;
    }
    
    public String getDuration() {
        return duration;
    }
    
    public int getTrackNumber() {
        return trackNumber;
    }
    
    public int getTotalTracks() {
        return totalTracks;
    }
    
    public Image getAlbumArt() {
        return albumArt;
    }
    
    /**
     * Retourne une représentation formatée de la position de la piste
     * @return Une chaîne au format "X/Y" où X est le numéro de piste et Y le total
     */
    public String getTrackPosition() {
        return trackNumber + "/" + totalTracks;
    }
    
    @Override
    public boolean equals(Object o) {
        if (this == o) return true;
        if (o == null || getClass() != o.getClass()) return false;
        TrackInfo trackInfo = (TrackInfo) o;
        return trackNumber == trackInfo.trackNumber &&
                Objects.equals(title, trackInfo.title) &&
                Objects.equals(artist, trackInfo.artist) &&
                Objects.equals(album, trackInfo.album);
    }
    
    @Override
    public int hashCode() {
        return Objects.hash(title, artist, album, trackNumber);
    }
    
    @Override
    public String toString() {
        return "TrackInfo{" +
                "title='" + title + '\'' +
                ", artist='" + artist + '\'' +
                ", album='" + album + '\'' +
                ", trackNumber=" + trackNumber +
                "/" + totalTracks +
                ", duration='" + duration + '\'' +
                "}";
    }
}